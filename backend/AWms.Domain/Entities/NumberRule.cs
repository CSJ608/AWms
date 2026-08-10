namespace AWms.Domain.Entities;

public class NumberRule
{
    public string Type { get; set; } = string.Empty;
    public string ScopeKey { get; set; } = "GLOBAL";
    public string? Prefix { get; set; }
    public string? DateFormat { get; set; }
    public int SeqLength { get; set; } = 4;
    public NumberResetPeriod ResetPeriod { get; set; } = NumberResetPeriod.DAILY;
    public NumberExhaustion OnExhaustion { get; set; } = NumberExhaustion.THROW;
    public int MaxValue { get; set; }
    /// <summary>动态作用域模板：scopeKey 由调用方提供（如 BATCH 按物料），注册键中 ScopeKey 仅作模板名。</summary>
    public bool DynamicScope { get; set; }
}

