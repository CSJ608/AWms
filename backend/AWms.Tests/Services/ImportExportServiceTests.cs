using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Domain.Interfaces;
using AWms.Infrastructure.Data;
using AWms.Infrastructure.Services;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AWms.Tests.Services;

public class ImportExportServiceTests
{
    private sealed class FakeNumberService : INumberService
    {
        private int _n;
        public Task<string> NextAsync(string type, string? scopeKey = null) => Task.FromResult($"IMP-20260810-{++_n:0000}");
        public Task<IReadOnlyList<string>> NextNAsync(string type, int count, string? scopeKey = null) =>
            Task.FromResult<IReadOnlyList<string>>(Enumerable.Range(1, count).Select(i => $"IMP-20260810-{i:0000}").ToList());
    }

    private static AWmsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AWmsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AWmsDbContext(options);
    }

    private static ImportExportService CreateService(AWmsDbContext db) =>
        new(db, new FakeNumberService(), new QueryService(), null!, NullLogger<ImportExportService>.Instance);

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
        ws.Cell(2, 1).Value = "MAT-001";
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
    public async Task PrecheckAsync_干净文件_canExecute为true且不落业务数据()
    {
        var db = CreateDb();
        var service = CreateService(db);

        var result = await service.PrecheckAsync("materials", BuildWorkbook(), "materials-import.xlsx", null);

        Assert.True(result.CanExecute);
        Assert.Equal("PRECHECKED", result.Status);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(0, result.FailCount);
        Assert.NotNull(result.Failures); // 契约：failures 固定 inline 返回
        Assert.Empty(result.Failures);
        // 业务数据不落库（复验意见：precheck 不落业务数据）
        Assert.Equal(0, await db.Materials.CountAsync());
        // 任务留痕存在
        Assert.Equal(1, await db.ImportTasks.CountAsync());
    }

    [Fact]
    public async Task PrecheckAsync_文件内重复编码_失败明细inline且canExecute为false()
    {
        var db = CreateDb();
        var service = CreateService(db);
        var file = BuildWorkbook(ws =>
        {
            ws.Cell(3, 1).Value = "MAT-001"; // 与第 2 行重复
            ws.Cell(3, 2).Value = "重复物料";
            ws.Cell(3, 4).Value = "FALSE";
            ws.Cell(3, 5).Value = "NONE";
            ws.Cell(3, 6).Value = "CT";
        });

        var result = await service.PrecheckAsync("materials", file, "materials-import.xlsx", null);

        Assert.False(result.CanExecute);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.FailCount);
        Assert.NotNull(result.Failures);
        var failure = Assert.Single(result.Failures!);
        Assert.Equal("MATERIAL_CODE_DUPLICATED", failure.ErrorCode);
        Assert.NotNull(result.FailReportUrl);
    }

    [Fact]
    public async Task PrecheckAsync_与库中重复_报错()
    {
        var db = CreateDb();
        db.Materials.Add(new Material { Code = "MAT-001", Name = "已有" });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.PrecheckAsync("materials", BuildWorkbook(), "materials-import.xlsx", null);

        Assert.False(result.CanExecute);
        Assert.Equal(1, result.FailCount);
        Assert.Equal("MATERIAL_CODE_DUPLICATED", result.Failures![0].ErrorCode);
    }

    [Fact]
    public async Task PrecheckAsync_非法枚举_失败明细()
    {
        var db = CreateDb();
        var service = CreateService(db);
        var file = BuildWorkbook(ws => ws.Cell(2, 5).Value = "BOGUS");

        var result = await service.PrecheckAsync("materials", file, "materials-import.xlsx", null);

        Assert.False(result.CanExecute);
        Assert.Contains(result.Failures!, f => f.ColumnCode == "labelType" && f.ErrorCode == "VALIDATION_ERROR");
    }

    [Fact]
    public async Task PrecheckAsync_超出200条_只inline前200()
    {
        var db = CreateDb();
        var service = CreateService(db);
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("数据");
        for (var r = 1; r <= 210; r++)
        {
            ws.Cell(r, 1).Value = r == 1 ? "code" : $"DUP-{r % 2}";
            ws.Cell(r, 2).Value = "名称";
            ws.Cell(r, 4).Value = "FALSE";
            ws.Cell(r, 5).Value = "NONE";
            ws.Cell(r, 6).Value = "CT";
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        var result = await service.PrecheckAsync("materials", ms.ToArray(), "big.xlsx", null);

        Assert.Equal(209, result.TotalCount);
        Assert.True(result.FailCount > 200);
        Assert.Equal(200, result.Failures!.Count);
    }

    [Fact]
    public async Task PrecheckAsync_不支持的模块_抛400()
    {
        var db = CreateDb();
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.PrecheckAsync("warehouses", BuildWorkbook(), "x.xlsx", null));
        Assert.Equal("VALIDATION_ERROR", ex.Code);
        Assert.Equal(400, ex.StatusCode);
    }
}


