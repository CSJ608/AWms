using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Domain.Interfaces;
using AWms.Infrastructure.Data;
using AWms.Infrastructure.Services;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AWms.IntegrationTests;

/// <summary>
/// 导入导出 E2E（真实 PostgreSQL，不 Skip）：
/// - precheck 不落业务数据 → execute 同事务重校验 + 真实入库；
/// - 导出 filter/sort/pageSize 生效、PROCESSING→DONE、独立作用域后台执行。
/// </summary>
// 每个测试类一个容器实例（规范 §5.4），避免跨类数据污染
public class ImportExportIntegrationTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public ImportExportIntegrationTests(PostgreSqlFixture fixture) => _fixture = fixture;

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.AddDbContext<AWmsDbContext>(o =>
            o.UseNpgsql(_fixture.ConnectionString, npgsql => npgsql.MigrationsAssembly("AWms.Infrastructure")));
        services.AddScoped<NumberingService>();
        services.AddScoped<INumberService>(sp => sp.GetRequiredService<NumberingService>());
        services.AddScoped<IQueryService, QueryService>();
        services.AddScoped<ImportExportService>();
        return services.BuildServiceProvider();
    }

    private static byte[] BuildWorkbook(Action<IXLWorksheet>? mutate = null)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("数据");
        ws.Cell(1, 1).Value = "物料编码";
        ws.Cell(1, 2).Value = "物料名称";
        ws.Cell(1, 3).Value = "助记码";
        ws.Cell(1, 4).Value = "批次管控";
        ws.Cell(1, 5).Value = "标签类型";
        ws.Cell(1, 6).Value = "默认单位";
        ws.Cell(1, 7).Value = "默认每签数量";
        ws.Cell(2, 1).Value = "E2E-MAT-001";
        ws.Cell(2, 2).Value = "螺母 M6";
        ws.Cell(2, 3).Value = "LM";
        ws.Cell(2, 4).Value = "TRUE";
        ws.Cell(2, 5).Value = "SKU";
        ws.Cell(2, 6).Value = "CT";
        ws.Cell(2, 7).Value = "10.0000";
        mutate?.Invoke(ws);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    [Fact]
    public async Task 导入E2E_precheck不落库_execute真实入库()
    {
        var prefix = Guid.CreateVersion7().ToString("N");
        using var provider = BuildProvider();
        await using var db = provider.GetRequiredService<AWmsDbContext>();
        var service = provider.GetRequiredService<ImportExportService>();

        var file = BuildWorkbook(ws =>
        {
            ws.Cell(2, 1).Value = $"{prefix}-01";
            ws.Cell(3, 1).Value = $"{prefix}-02";
            ws.Cell(3, 2).Value = "螺栓 M8";
            ws.Cell(3, 4).Value = "FALSE";
            ws.Cell(3, 5).Value = "NONE";
            ws.Cell(3, 6).Value = "CT";
        });

        // precheck：不落业务数据
        var pre = await service.PrecheckAsync("materials", file, "e2e-import.xlsx", null);
        Assert.True(pre.CanExecute);
        Assert.Equal(0, await db.Materials.CountAsync(m => m.Code.StartsWith(prefix)));

        // execute：真实入库
        var executed = await service.ExecuteAsync(pre.Id);
        Assert.Equal("DONE", executed.Status);
        Assert.Equal(2, executed.SuccessCount);
        Assert.Equal(2, await db.Materials.CountAsync(m => m.Code.StartsWith(prefix)));
        var saved = await db.Materials.FirstAsync(m => m.Code == $"{prefix}-01");
        Assert.Equal("螺母 M6", saved.Name);
        Assert.Equal(LabelType.SKU, saved.LabelType);
        Assert.Equal(10m, saved.DefaultQtyPerLabel);
    }

    [Fact]
    public async Task 导入E2E_库中重复码_precheck拒绝_execute422()
    {
        var prefix = Guid.CreateVersion7().ToString("N");
        using var provider = BuildProvider();
        await using var db = provider.GetRequiredService<AWmsDbContext>();
        var service = provider.GetRequiredService<ImportExportService>();

        db.Materials.Add(new Material { Code = $"{prefix}-01", Name = "已存在" });
        await db.SaveChangesAsync();

        var pre = await service.PrecheckAsync("materials", BuildWorkbook(ws => ws.Cell(2, 1).Value = $"{prefix}-01"), "dup.xlsx", null);
        Assert.False(pre.CanExecute);
        Assert.Equal("MATERIAL_CODE_DUPLICATED", pre.Failures![0].ErrorCode);

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.ExecuteAsync(pre.Id));
        Assert.Equal("IMPORT_VALIDATION_FAILED", ex.Code);
        Assert.Equal(422, ex.StatusCode);
        Assert.Equal(1, await db.Materials.CountAsync(m => m.Code == $"{prefix}-01")); // 仅原有，无新增
    }

    [Fact]
    public async Task 导出E2E_filterSortPageSize生效_状态PROCESSING到DONE()
    {
        var prefix = Guid.CreateVersion7().ToString("N");
        using var provider = BuildProvider();
        await using var db = provider.GetRequiredService<AWmsDbContext>();
        var service = provider.GetRequiredService<ImportExportService>();

        // 同容器内测试隔离：清空物料
        db.Materials.RemoveRange(db.Materials);
        await db.SaveChangesAsync();
        db.Materials.AddRange(
            new Material { Code = $"{prefix}-A", Name = "启用A", Status = MaterialStatus.ENABLED },
            new Material { Code = $"{prefix}-B", Name = "停用B", Status = MaterialStatus.DISABLED },
            new Material { Code = $"{prefix}-C", Name = "启用C", Status = MaterialStatus.ENABLED });
        await db.SaveChangesAsync();

        var filter = new AWms.Domain.Dtos.Common.FilterGroup("and", new List<AWms.Domain.Dtos.Common.FilterCondition>
        {
            new("status", "eq", "ENABLED")
        });
        var request = new AWms.Domain.Dtos.Common.FilterRequest(null, null, null, null, null, null, null, null,
            new List<AWms.Domain.Dtos.Common.SortOption> { new("code", "asc") }, filter, 1, 2);

        var created = await service.CreateExportAsync("materials", request, null, "集成测试");
        Assert.Equal("PROCESSING", created.Status);

        // 轮询直到 DONE（后台独立作用域执行；不 sleep 用轮询+超时）
        var deadline = DateTime.UtcNow.AddSeconds(30);
        AWms.Domain.Dtos.ImportExport.ImportTaskResponse? done = null;
        while (DateTime.UtcNow < deadline)
        {
            var current = await service.GetTaskAsync(created.Id);
            if (current != null && current.Status is "DONE" or "FAILED")
            {
                done = current;
                break;
            }
            await Task.Delay(200);
        }

        Assert.NotNull(done);
        Assert.Equal("DONE", done!.Status);
        Assert.Equal(2, done.TotalCount); // filter=ENABLED + pageSize=2 → 2 条（启用A、启用C）
        var (data, _) = await service.GetTaskFileAsync(created.Id);
        Assert.NotNull(data);
        Assert.True(data!.Length > 0);

        // 校验导出文件内容行数 = 2 条数据 + 表头
        using var ms = new MemoryStream(data);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet(1);
        Assert.Equal("启用A", ws.Cell(2, 2).GetString());
        Assert.Equal("启用C", ws.Cell(3, 2).GetString());
        Assert.True(ws.Cell(4, 1).IsEmpty());
    }

    [Fact]
    public async Task 导出E2E_pageSize为0_全量导出()
    {
        var prefix = Guid.CreateVersion7().ToString("N");
        using var provider = BuildProvider();
        await using var db = provider.GetRequiredService<AWmsDbContext>();
        var service = provider.GetRequiredService<ImportExportService>();

        db.Materials.RemoveRange(db.Materials);
        await db.SaveChangesAsync();
        db.Materials.AddRange(
            new Material { Code = $"{prefix}-A", Name = "甲" },
            new Material { Code = $"{prefix}-B", Name = "乙" });
        await db.SaveChangesAsync();

        var request = new AWms.Domain.Dtos.Common.FilterRequest(null, null, null, null, null, null, null, null, null, null, 1, 0);
        var created = await service.CreateExportAsync("materials", request, null, null);

        var deadline = DateTime.UtcNow.AddSeconds(30);
        AWms.Domain.Dtos.ImportExport.ImportTaskResponse? done = null;
        while (DateTime.UtcNow < deadline)
        {
            var current = await service.GetTaskAsync(created.Id);
            if (current != null && current.Status is "DONE" or "FAILED")
            {
                done = current;
                break;
            }
            await Task.Delay(200);
        }

        Assert.NotNull(done);
        Assert.Equal("DONE", done!.Status);
        Assert.Equal(2, done.TotalCount);
    }
}




