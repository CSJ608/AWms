using AWms.Domain.Entities;
using AWms.Domain.Enums;
using AWms.Infrastructure.Data;
using AWms.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace AWms.Tests.Services;

public class NumberingServiceTests
{
    private static AWmsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AWmsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AWmsDbContext(options);
    }

    [Fact]
    public void RegisterDefaults_HasAllRequiredTypes()
    {
        var service = new NumberingService(CreateDb());

        Assert.Contains(service.Rules.Keys, k => k.StartsWith("IMPORT_TASK"));
        Assert.Contains(service.Rules.Keys, k => k.StartsWith("BATCH"));
        Assert.Contains(service.Rules.Keys, k => k.StartsWith("TXN_GROUP"));
        Assert.Contains(service.Rules.Keys, k => k.StartsWith("RECEIPT"));
        Assert.Contains(service.Rules.Keys, k => k.StartsWith("INBOUND_ORDER"));
    }

    [Fact]
    public void BATCH_Rule_Format_IsDatePlusSeq()
    {
        var service = new NumberingService(CreateDb());

        var batchRule = service.Rules["BATCH:MATERIAL"];
        Assert.Null(batchRule.Prefix);
        Assert.Equal("yyMMdd", batchRule.DateFormat);
        Assert.Equal(3, batchRule.SeqLength);
        Assert.Equal(999, batchRule.MaxValue);
    }

    [Fact]
    public void TXN_GROUP_Rule_Has9DigitSeq()
    {
        var service = new NumberingService(CreateDb());

        var rule = service.Rules["TXN_GROUP:GLOBAL"];
        Assert.Equal(9, rule.SeqLength);
        Assert.Equal(999999999, rule.MaxValue);
    }

    [Fact]
    public void IMPORT_TASK_Rule_HasIMPPrefix()
    {
        var service = new NumberingService(CreateDb());

        var rule = service.Rules["IMPORT_TASK:GLOBAL"];
        Assert.Equal("IMP", rule.Prefix);
        Assert.Equal("yyyyMMdd", rule.DateFormat);
        Assert.Equal(4, rule.SeqLength);
    }

    [Fact]
    public void FormatNumber_BATCH_260810001()
    {
        var rule = new NumberRule { Type = "BATCH", ScopeKey = "MATERIAL", Prefix = null, DateFormat = "yyMMdd", SeqLength = 3 };

        var result = NumberingService.FormatNumber(rule, new DateOnly(2026, 8, 10), 1);

        Assert.Equal("260810001", result);
    }

    [Fact]
    public void FormatNumber_TXN_GROUP_15位()
    {
        var rule = new NumberRule { Type = "TXN_GROUP", ScopeKey = "GLOBAL", Prefix = null, DateFormat = "yyMMdd", SeqLength = 9 };

        var result = NumberingService.FormatNumber(rule, new DateOnly(2026, 8, 10), 1);

        Assert.Equal("260810000000001", result);
        Assert.Equal(15, result.Length);
    }

    [Fact]
    public void FormatNumber_IMPORT_TASK_IMP前缀()
    {
        var rule = new NumberRule { Type = "IMPORT_TASK", ScopeKey = "GLOBAL", Prefix = "IMP", DateFormat = "yyyyMMdd", SeqLength = 4 };

        var result = NumberingService.FormatNumber(rule, new DateOnly(2026, 8, 10), 1);

        Assert.Equal("IMP-20260810-0001", result);
    }

    [Fact]
    public void FormatNumber_序号补零()
    {
        var rule = new NumberRule { Type = "RECEIPT", ScopeKey = "GLOBAL", Prefix = "RCP", DateFormat = "yyyyMMdd", SeqLength = 4 };

        Assert.Equal("RCP-20260810-0042", NumberingService.FormatNumber(rule, new DateOnly(2026, 8, 10), 42));
    }

    [Fact]
    public async Task NextAsync_未注册规则_抛VALIDATION_ERROR()
    {
        var service = new NumberingService(CreateDb());

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.NextAsync("NONEXISTENT_TYPE"));
        Assert.Equal("VALIDATION_ERROR", ex.Code);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void Register_规则MaxValue非正_拒绝()
    {
        var service = new NumberingService(CreateDb());

        Assert.Throws<ArgumentException>(() =>
            service.Register(new NumberRule { Type = "X", ScopeKey = "Y", MaxValue = 0 }));
    }
}
