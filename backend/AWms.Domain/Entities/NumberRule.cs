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
}
