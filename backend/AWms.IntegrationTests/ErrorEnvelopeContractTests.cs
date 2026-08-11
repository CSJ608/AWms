using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AWms.Domain.Dtos.Common;
using ClosedXML.Excel;

namespace AWms.IntegrationTests;

/// <summary>
/// 契约测试（docs/api/README.md 通用规范 2.1）：错误响应 envelope 顶层键统一小写 code/message/data，
/// 禁止 PascalCase（修复 Q1：ExceptionHandlerMiddleware 与 MVC/过滤器路径输出一致）。
/// </summary>
public class ErrorEnvelopeContractTests : IClassFixture<ApiTestFixture>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly ApiTestFixture _fixture;
    private readonly HttpClient _client;

    public ErrorEnvelopeContractTests(ApiTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    private async Task LoginAdminAsync()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = ApiTestFixture.AdminUsername,
            password = ApiTestFixture.AdminPassword
        });
        resp.EnsureSuccessStatusCode();
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOpts);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", envelope!.Data!.Token);
    }

    /// <summary>断言错误 envelope：顶层只有小写 code/message/data，且不含任何 PascalCase 键。</summary>
    private static async Task AssertLowercaseEnvelopeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.True(root.TryGetProperty("code", out var code), "错误 envelope 顶层应含小写 code，实际：" + body);
        Assert.True(root.TryGetProperty("message", out _), "错误 envelope 顶层应含小写 message，实际：" + body);
        Assert.True(root.TryGetProperty("data", out _), "错误 envelope 顶层应含小写 data，实际：" + body);
        Assert.False(root.TryGetProperty("Code", out _), "错误 envelope 不得含 PascalCase Code，实际：" + body);
        Assert.False(root.TryGetProperty("Message", out _), "错误 envelope 不得含 PascalCase Message，实际：" + body);
        Assert.False(root.TryGetProperty("Data", out _), "错误 envelope 不得含 PascalCase Data，实际：" + body);
        Assert.False(string.IsNullOrEmpty(code.GetString()), "code 不得为空");
    }

    [Fact]
    public async Task 登录失败401_错误envelope全小写()
    {
        await _fixture.ResetDatabaseAsync();
        var resp = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = ApiTestFixture.AdminUsername,
            password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        await AssertLowercaseEnvelopeAsync(resp);
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOpts);
        Assert.Equal("LOGIN_FAILED", envelope!.Code);
    }

    [Fact]
    public async Task 重复码409_错误envelope全小写()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();
        var prefix = Guid.CreateVersion7().ToString("N");

        var first = await _client.PostAsJsonAsync("/api/materials", new
        {
            code = $"{prefix}-01", name = "物料一", batchControlled = false, labelType = "NONE", defaultUom = "CT"
        });
        first.EnsureSuccessStatusCode();

        var dup = await _client.PostAsJsonAsync("/api/materials", new
        {
            code = $"{prefix}-01", name = "重复", batchControlled = false, labelType = "NONE", defaultUom = "CT"
        });

        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        await AssertLowercaseEnvelopeAsync(dup);
        var envelope = await dup.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOpts);
        Assert.Equal("MATERIAL_CODE_DUPLICATED", envelope!.Code);
    }

    [Fact]
    public async Task 导入拒绝422_错误envelope全小写()
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

        var exec = await _client.PostAsJsonAsync("/api/import-export/execute", new { taskId = preEnvelope!.Data!.Id });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, exec.StatusCode);
        await AssertLowercaseEnvelopeAsync(exec);
        var envelope = await exec.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOpts);
        Assert.Equal("IMPORT_VALIDATION_FAILED", envelope!.Code);
    }

    [Fact]
    public async Task 权限403_错误envelope全小写()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();

        var created = await _client.PostAsJsonAsync("/api/users", new
        {
            username = "operator01",
            name = "作业员",
            password = "Pass123!",
            roleIds = new[] { (await GetRoleIdAsync("OPERATOR")) }
        });
        created.EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { username = "operator01", password = "Pass123!" });
        login.EnsureSuccessStatusCode();
        var loginEnvelope = await login.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(JsonOpts);
        var opClient = _fixture.Factory.CreateClient();
        opClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginEnvelope!.Data!.Token);

        var resp = await opClient.PostAsJsonAsync("/api/materials", new
        {
            code = "MAT-FB-001", name = "越权物料", batchControlled = false, labelType = "NONE", defaultUom = "CT"
        });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        await AssertLowercaseEnvelopeAsync(resp);
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOpts);
        Assert.Equal("FORBIDDEN", envelope!.Code);
    }

    [Fact]
    public async Task 资源404_错误envelope全小写()
    {
        await _fixture.ResetDatabaseAsync();
        await LoginAdminAsync();

        var resp = await _client.GetAsync("/api/meta/fields/unknown-resource");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        await AssertLowercaseEnvelopeAsync(resp);
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOpts);
        Assert.Equal("NOT_FOUND", envelope!.Code);
    }

    private async Task<Guid> GetRoleIdAsync(string code)
    {
        var resp = await _client.GetAsync("/api/roles");
        resp.EnsureSuccessStatusCode();
        var envelope = await resp.Content.ReadFromJsonAsync<ApiResponse<List<RoleItem>>>(JsonOpts);
        return envelope!.Data!.Single(r => r.Code == code).Id;
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

    private record LoginResponse(string Token, DateTime ExpiresAt, object User, List<string> Permissions, object Menus);
    private record RoleItem(Guid Id, string Code, string Name, List<string> PermissionCodes, DateTime CreatedAt);
    private record ImportTaskResponse(Guid Id, string TaskNo, string ModuleCode, string Status, bool CanExecute, List<object>? Failures);
}