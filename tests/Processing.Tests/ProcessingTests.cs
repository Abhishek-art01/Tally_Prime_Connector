using TallyPrimeConnector.Contracts;
using TallyPrimeConnector.Processing;
namespace Processing.Tests;
public sealed class ProcessingTests { [Fact] public async Task Totals_respect_debit_credit_direction() { var v = new VoucherInfo("1", "1", new DateOnly(2026, 4, 1), "Receipt", null, [new("a", "Cash", new(100m, DebitCredit.Debit)), new("b", "Sales", new(100m, DebitCredit.Credit))]); var r = await new BasicValidationProcessor().ProcessAsync([v], CancellationToken.None); Assert.Equal(100m, r.DebitTotal); Assert.Equal(100m, r.CreditTotal); } }
