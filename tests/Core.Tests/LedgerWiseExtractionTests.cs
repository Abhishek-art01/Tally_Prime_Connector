using Microsoft.Extensions.Logging.Abstractions;
using TallyPrimeConnector.Contracts;
using TallyPrimeConnector.Core;

namespace Core.Tests;

public sealed class LedgerWiseExtractionTests
{
    [Fact]
    public void Batch_planner_covers_requested_days_exactly_once()
    {
        var batches = DateBatchPlanner.Create(DateRange.Create(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30)), 7);

        Assert.Equal(5, batches.Count);
        Assert.Equal(new DateOnly(2026, 4, 1), batches[0].From);
        Assert.Equal(new DateOnly(2026, 4, 30), batches[^1].To);
        Assert.All(batches.Zip(batches.Skip(1)), pair => Assert.Equal(pair.First.To.AddDays(1), pair.Second.From));
        Assert.Single(DateBatchPlanner.Create(DateRange.Create(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1)), 31));
    }

    [Fact]
    public void Ledger_export_request_rejects_missing_ledgers_and_invalid_output()
    {
        var context = new CompanyContext(new CompanyInfo("company", "Company"), Profile());
        var range = DateRange.Create(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1));

        Assert.Throws<ExtractionException>(() => LedgerWiseExtractionRequest.Create(context, range, null, [], Path.Combine(Path.GetTempPath(), "output.xlsx")));
        Assert.Throws<ExportException>(() => LedgerWiseExtractionRequest.Create(context, range, null, [new("l", "Cash")], Path.Combine(Path.GetTempPath(), "output.csv")));
        Assert.Throws<ExportException>(() => LedgerWiseExtractionRequest.Create(context, range, null, [new("l", "Cash")], "Z:\\not-a-directory\\output.xlsx"));
    }

    [Fact]
    public async Task Extracts_exact_ledger_membership_in_batches_and_reports_progress()
    {
        var provider = new StubVoucherService(
            new VoucherInfo("1", "V-1", new DateOnly(2026, 9, 1), "Journal", null, [Entry("Bank", -10m, DebitCredit.Debit), Entry("Bank Charges", 10m, DebitCredit.Credit)], Guid: "g1", MasterId: "m1"),
            new VoucherInfo("2", "V-2", new DateOnly(2026, 9, 3), "Journal", null, [Entry("Cash", -5m, DebitCredit.Debit), Entry("Bank", 5m, DebitCredit.Credit)], Guid: "g2", MasterId: "m2"));
        var progress = new List<ExtractionProgress>();
        var service = new LedgerWiseExtractionService(provider, NullLogger<LedgerWiseExtractionService>.Instance);

        var result = await service.ExtractAsync(Request(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), 2, [new("bank", "Bank"), new("empty", "No Entries")]), new InlineProgress(progress), CancellationToken.None);

        Assert.Equal(2, provider.Calls.Count);
        Assert.Equal(2, result.TotalVoucherCount);
        Assert.Equal(2, result.TotalMatchedTransactionCount);
        var bank = Assert.Single(result.Ledgers, x => x.Ledger.Name == "Bank");
        Assert.Equal(2, bank.TransactionCount);
        Assert.Equal(10m, bank.DebitTotal);
        Assert.Equal(5m, bank.CreditTotal);
        Assert.Empty(Assert.Single(result.Ledgers, x => x.Ledger.Name == "No Entries").Transactions);
        Assert.Equal(ReconciliationStatus.Balanced, result.Reconciliation.Status);
        Assert.Contains(progress, x => x.CurrentBatch == 1 && x.TotalBatches == 2);
        Assert.True(progress.Last().IsCompleted);
    }

    [Fact]
    public async Task Deduplicates_same_guid_but_rejects_conflicting_content()
    {
        var duplicate = new VoucherInfo("s1", "V-1", new DateOnly(2026, 9, 1), "Journal", null, [Entry("Cash", -1m, DebitCredit.Debit), Entry("Bank", 1m, DebitCredit.Credit)], Guid: "same", MasterId: "m1");
        var service = new LedgerWiseExtractionService(new StubVoucherService(duplicate, duplicate), NullLogger<LedgerWiseExtractionService>.Instance);
        var result = await service.ExtractAsync(Request(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), 1, [new("cash", "Cash")]), null, CancellationToken.None);
        Assert.Equal(1, result.TotalVoucherCount);

        var conflicting = duplicate with { Entries = [Entry("Cash", -2m, DebitCredit.Debit), Entry("Bank", 2m, DebitCredit.Credit)] };
        var conflictService = new LedgerWiseExtractionService(new StubVoucherService(duplicate, conflicting), NullLogger<LedgerWiseExtractionService>.Instance);
        await Assert.ThrowsAsync<ExtractionException>(() => conflictService.ExtractAsync(Request(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), 1, [new("cash", "Cash")]), null, CancellationToken.None));
    }

    [Fact]
    public async Task Deduplicates_master_id_then_careful_fallback_identity()
    {
        var master = new VoucherInfo("source-a", "V-1", new DateOnly(2026, 9, 1), "Journal", "same", [Entry("Cash", -1.01m, DebitCredit.Debit), Entry("Bank", 1.01m, DebitCredit.Credit)], MasterId: "master-1");
        var fallback = new VoucherInfo("source-b", "V-2", new DateOnly(2026, 9, 1), "Journal", "same", [Entry("Cash", -2.02m, DebitCredit.Debit), Entry("Bank", 2.02m, DebitCredit.Credit)]);
        var service = new LedgerWiseExtractionService(new StubVoucherService(master, master, fallback, fallback), NullLogger<LedgerWiseExtractionService>.Instance);

        var result = await service.ExtractAsync(Request(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), 1, [new("cash", "Cash")]), null, CancellationToken.None);

        Assert.Equal(2, result.TotalVoucherCount);
        Assert.Equal(3.03m, Assert.Single(result.Ledgers).DebitTotal);
    }

    [Fact]
    public async Task Rejects_an_out_of_range_voucher_even_when_a_provider_breaks_its_contract()
    {
        var unsafeProvider = new UnsafeVoucherService(new VoucherInfo("1", "V-10", new DateOnly(2026, 9, 10), "Journal", null, [Entry("Cash", -1m, DebitCredit.Debit), Entry("Bank", 1m, DebitCredit.Credit)], Guid: "unsafe"));
        var service = new LedgerWiseExtractionService(unsafeProvider, NullLogger<LedgerWiseExtractionService>.Instance);

        await Assert.ThrowsAsync<TallyProtocolException>(() => service.ExtractAsync(Request(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), 1, [new("cash", "Cash")]), null, CancellationToken.None));
    }

    [Fact]
    public async Task Ledger_transactions_are_ordered_by_date_voucher_and_entry_position()
    {
        var later = new VoucherInfo("2", "V-2", new DateOnly(2026, 9, 2), "Journal", null, [Entry("Cash", -1m, DebitCredit.Debit), Entry("Bank", 1m, DebitCredit.Credit)], Guid: "later");
        var earlier = new VoucherInfo("1", "V-1", new DateOnly(2026, 9, 1), "Journal", null, [Entry("Bank", -2m, DebitCredit.Debit), Entry("Bank", 2m, DebitCredit.Credit)], Guid: "earlier");
        var service = new LedgerWiseExtractionService(new StubVoucherService(later, earlier), NullLogger<LedgerWiseExtractionService>.Instance);

        var result = await service.ExtractAsync(Request(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2), 2, [new("bank", "Bank")]), null, CancellationToken.None);
        var transactions = Assert.Single(result.Ledgers).Transactions;

        Assert.Equal(["V-1", "V-1", "V-2"], transactions.Select(x => x.Voucher.VoucherNumber));
        Assert.Equal([0, 1, 1], transactions.Select(x => x.LedgerEntryPosition));
    }

    [Fact]
    public async Task Cancellation_stops_before_future_batch()
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new StubVoucherService(new VoucherInfo("1", "V-1", new DateOnly(2026, 9, 1), "Journal", null, [Entry("Cash", -1m, DebitCredit.Debit), Entry("Bank", 1m, DebitCredit.Credit)], Guid: "one")) { OnCall = () => cancellation.Cancel() };
        var service = new LedgerWiseExtractionService(provider, NullLogger<LedgerWiseExtractionService>.Instance);

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.ExtractAsync(Request(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), 1, [new("cash", "Cash")]), null, cancellation.Token));
        Assert.Single(provider.Calls);
    }

    private static VoucherEntryInfo Entry(string ledger, decimal sourceAmount, DebitCredit direction) => new(ledger, ledger, new(Math.Abs(sourceAmount), direction), SourceAmount: sourceAmount);
    private static ConnectionProfile Profile() => new("test", "localhost", 9000, TallyProtocol.HttpXml, TallyConnectionMethod.XmlHttp);
    private static LedgerWiseExtractionRequest Request(DateOnly from, DateOnly to, int batchDays, IReadOnlyList<LedgerInfo> ledgers) => LedgerWiseExtractionRequest.Create(new CompanyContext(new CompanyInfo("company", "Company"), Profile()), DateRange.Create(from, to), null, ledgers, Path.Combine(Path.GetTempPath(), "tally-ledger-test.xlsx"), batchDays);

    private sealed class StubVoucherService(params VoucherInfo[] vouchers) : IVoucherService
    {
        public List<DateRange> Calls { get; } = [];
        public Action? OnCall { get; init; }
        public Task<IReadOnlyList<VoucherInfo>> GetVouchersAsync(CompanyInfo company, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            Calls.Add(DateRange.Create(from, to));
            OnCall?.Invoke();
            return Task.FromResult<IReadOnlyList<VoucherInfo>>(vouchers.Where(x => x.Date >= from && x.Date <= to).ToList());
        }
    }

    private sealed class InlineProgress(List<ExtractionProgress> values) : IProgress<ExtractionProgress>
    {
        public void Report(ExtractionProgress value) => values.Add(value);
    }

    private sealed class UnsafeVoucherService(VoucherInfo voucher) : IVoucherService
    {
        public Task<IReadOnlyList<VoucherInfo>> GetVouchersAsync(CompanyInfo company, DateOnly from, DateOnly to, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<VoucherInfo>>([voucher]);
    }
}
