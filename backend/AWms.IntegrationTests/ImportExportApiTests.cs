using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AWms.Domain.Dtos.Common;
using AWms.Domain.Dtos.ImportExport;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AWms.IntegrationTests;

/// <summary>导入导出 API E2E：模板/precheck/execute/异步导出任务轮询/文件下载。</summary>
public class ImportExportApiTests : IClassFixture<ApiTestFixture>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly ApiTestFixture _fixture;
    private readonly HttpClient _client;

    public ImportExportApiTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    private async Task LoginAdminAsync()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login", new { username = ApiTestFixture.AdminUsername, password = ApiTestFixture.AdminPassword });
        resp.EnsureSuccessStatusCode();
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOpts);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", envelope!.Data!.Token);
    }

    private static byte[] BuildWorkbook(params (string Code, string Name)[] rows)
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
        var r = 2;
        foreach (var (code, name) in rows)
        {
            ws.Cell(r, 1).Value = code;
            ws.Cell(r, 2).Value = name;
            ws.Cell(r, 3).Value = "";
            ws.Cell(r, 4).Value = "TRUE";
            ws.Cell(r, 5).Value = "SKU";
            ws.Cell(r, 6).Value = "CT";
            ws.Cell(r, 7).Value = "10.0000";
            r++;
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    [Fact]
    public async Task 导入E2E_模板_precheck_execute_真实入库()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var prefix = Guid.CreateVersion7().ToString("N");

        // 模板
        var tmpl = await _client.GetAsync("/api/import-export/templates/materials");
        tmpl.EnsureSuccessStatusCode();
        Assert.True((await tmpl.Content.ReadAsByteArrayAsync()).Length > 0);

        // precheck（multipart）
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("materials"), "moduleCode");
        content.Add(new ByteArrayContent(BuildWorkbook(($"{prefix}-01", "导入物料一"))), "file", "import.xlsx");
        var pre = await _client.PostAsync("/api/import-export/precheck", content);
        pre.EnsureSuccessStatusCode();
        var preEnvelope = await pre.Content.ReadFromJsonAsync<ApiResponse<ImportTaskResponse>>(JsonOpts);
        Assert.True(preEnvelope!.Data!.CanExecute);
        Assert.Equal("PRECHECKED", preEnvelope.Data.Status);

        // execute
        var exec = await _client.PostAsJsonAsync("/api/import-export/execute", new { taskId = preEnvelope.Data.Id });
        exec.EnsureSuccessStatusCode();
        var execEnvelope = await exec.Content.ReadFromJsonAsync<ApiResponse<ImportTaskResponse>>(JsonOpts);
        Assert.Equal("DONE", execEnvelope!.Data!.Status);

        // 库中可查
        var search = await _client.PostAsJsonAsync("/api/materials/search", new { keyword = prefix, page = 1, pageSize = 20 });
        search.EnsureSuccessStatusCode();
        var searchEnvelope = await search.Content.ReadFromJsonAsync<ApiResponse<PagedResult<MaterialItem>>>(JsonOpts);
        Assert.Equal(1, searchEnvelope!.Data!.Total);
        Assert.Equal($"{prefix}-01", searchEnvelope.Data.Items[0].Code);
    }

    [Fact]
    public async Task 导入E2E_文件内重复_precheck失败明细inline_execute422()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var prefix = Guid.CreateVersion7().ToString("N");

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("materials"), "moduleCode");
        content.Add(new ByteArrayContent(BuildWorkbook(($"{prefix}-01", "A"), ($"{prefix}-01", "B"))), "file", "dup.xlsx");
        var pre = await _client.PostAsync("/api/import-export/precheck", content);
        pre.EnsureSuccessStatusCode();
        var preEnvelope = await pre.Content.ReadFromJsonAsync<ApiResponse<ImportTaskResponse>>(JsonOpts);
        Assert.False(preEnvelope!.Data!.CanExecute);
        Assert.NotNull(preEnvelope.Data.Failures);
        Assert.Contains(preEnvelope.Data.Failures!, f => f.ErrorCode == "MATERIAL_CODE_DUPLICATED");

        var exec = await _client.PostAsJsonAsync("/api/import-export/execute", new { taskId = preEnvelope.Data.Id });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, exec.StatusCode);
        var execEnvelope = await exec.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOpts);
        Assert.Equal("IMPORT_VALIDATION_FAILED", execEnvelope!.Code);
    }

    [Fact]
    public async Task 导出E2E_创建PROCESSING_轮询DONE_下载文件()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var prefix = Guid.CreateVersion7().ToString("N");

        // 准备数据
        foreach (var (code, name) in new[] { ($"{prefix}-A", "导出甲"), ($"{prefix}-B", "导出乙") })
        {
            var resp = await _client.PostAsJsonAsync("/api/materials", new
            {
                code, name, batchControlled = false, labelType = "NONE", defaultUom = "CT"
            });
            resp.EnsureSuccessStatusCode();
        }

        // 创建导出任务（带 filter/pageSize）
        var export = await _client.PostAsJsonAsync("/api/import-export/export", new
        {
            moduleCode = "materials",
            filter = new
            {
                op = "and",
                conditions = new object[] { new { field = "code", op = "contains", value = prefix } }
            },
            pageSize = 1
        });
        Assert.Equal(HttpStatusCode.Created, export.StatusCode);
        var exportEnvelope = await export.Content.ReadFromJsonAsync<ApiResponse<ImportTaskResponse>>(JsonOpts);
        Assert.Equal("PROCESSING", exportEnvelope!.Data!.Status);

        // 轮询 DONE
        var deadline = DateTime.UtcNow.AddSeconds(30);
        ImportTaskResponse? done = null;
        while (DateTime.UtcNow < deadline)
        {
            var taskResp = await _client.GetAsync($"/api/import-export/tasks/{exportEnvelope.Data.Id}");
            taskResp.EnsureSuccessStatusCode();
            var taskEnvelope = await taskResp.Content.ReadFromJsonAsync<ApiResponse<ImportTaskResponse>>(JsonOpts);
            if (taskEnvelope!.Data!.Status is "DONE" or "FAILED")
            {
                done = taskEnvelope.Data;
                break;
            }
            await Task.Delay(200);
        }

        Assert.NotNull(done);
        Assert.Equal("DONE", done!.Status);
        Assert.Equal(1, done.TotalCount); // pageSize=1

        var file = await _client.GetAsync($"/api/import-export/tasks/{exportEnvelope.Data.Id}/file");
        file.EnsureSuccessStatusCode();
        Assert.True((await file.Content.ReadAsByteArrayAsync()).Length > 0);
    }

    private record LoginResponse(string Token, DateTime ExpiresAt, object User, List<string> Permissions, object Menus);
    private record MaterialItem(Guid Id, string Code, string Name, string? SearchCode, bool BatchControlled, string LabelType, string DefaultUom, string? DefaultQtyPerLabel, string Status, DateTime CreatedAt, DateTime UpdatedAt);
}
