namespace Data.Tests;
public sealed class FinancialPrecisionTests { [Fact] public void Decimal_preserves_financial_precision() => Assert.Equal(0.30m, 0.10m + 0.20m); [Fact] public void DateOnly_is_calendar_safe() => Assert.Equal(new DateOnly(2026, 4, 1), DateOnly.Parse("2026-04-01")); }
