using TallyPrimeConnector.Contracts;
using TallyPrimeConnector.Core;
using TallyPrimeConnector.Tally;
namespace Core.Tests;
public sealed class DiscoveryTests
{
    [Fact] public void Invalid_date_range_is_rejected() => Assert.Throws<ArgumentException>(() => DateRange.Create(new DateOnly(2026, 4, 2), new DateOnly(2026, 4, 1)));
    [Fact] public async Task Mock_extraction_reports_voucher_sample() { var service = new ExtractionService(new VoucherService(new MockTallyVoucherProvider())); var result = await service.ExtractAsync(new(new("mock", "Demo", new DateTime(2026, 4, 1)), new("Sundry Debtors", ["Acme Trading"]), DateRange.Create(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 1))), null, CancellationToken.None); Assert.Single(result.Vouchers); Assert.Equal(2500.25m, result.Vouchers[0].Entries[0].Amount.Value); }
}
