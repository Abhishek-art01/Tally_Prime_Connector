using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using TallyPrimeConnector.Contracts;

namespace TallyPrimeConnector.Core;

/// <summary>Plans adjacent inclusive date ranges without gaps or overlaps.</summary>
public static class DateBatchPlanner
{
    public static IReadOnlyList<DateRange> Create(DateRange requestedRange, int batchDays)
    {
        if (batchDays < 1) throw new ExtractionException("Batch size must be at least one day.");

        var batches = new List<DateRange>();
        var from = requestedRange.From;
        while (from <= requestedRange.To)
        {
            var remainingDays = requestedRange.To.DayNumber - from.DayNumber;
            var to = from.AddDays(Math.Min(batchDays - 1, remainingDays));
            batches.Add(DateRange.Create(from, to));
            if (to == requestedRange.To) break;
            from = to.AddDays(1);
        }
        return batches;
    }
}

/// <summary>
/// Retrieves bounded, already date-validated voucher batches and derives exact ledger membership in C#.
/// Group is workflow metadata only: this service never claims Tally performed a server-side group filter.
/// </summary>
public sealed class LedgerWiseExtractionService(IVoucherService vouchers, ILogger<LedgerWiseExtractionService> logger) : ILedgerWiseExtractionService
{
    public async Task<LedgerWiseExtractionResult> ExtractAsync(LedgerWiseExtractionRequest request, IProgress<ExtractionProgress>? progress, CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var startedAt = DateTimeOffset.UtcNow;
        var batches = DateBatchPlanner.Create(request.DateRange, request.BatchDays);
        var selectedLedgers = request.SelectedLedgers
            .GroupBy(x => CanonicalLedgerName(x.Name), StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var transactions = selectedLedgers.Keys.ToDictionary(x => x, _ => new List<LedgerTransaction>(), StringComparer.Ordinal);
        // Keep only identity/fingerprint state for non-selected vouchers; worksheet rows retain selected-ledger transactions.
        // This bounds retained data to output requirements rather than every voucher object in a broad requested period.
        var uniqueVoucherFingerprints = new Dictionary<string, string>(StringComparer.Ordinal);
        var batchResults = new List<ExtractionBatch>(batches.Count);
        var totalMatched = 0;
        var overallDebit = 0m;
        var overallCredit = 0m;
        var unbalancedVoucherIdentities = new List<string>();

        logger.LogInformation("Ledger export started for {Company}; {FromDate} to {ToDate}; {LedgerCount} ledgers; {BatchCount} batches.", request.CompanyContext.Company.Name, request.DateRange.From, request.DateRange.To, selectedLedgers.Count, batches.Count);
        progress?.Report(new ExtractionProgress("Preparing bounded extraction", 0, 0, 0, false, 0, batches.Count));

        try
        {
            for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = batches[batchIndex];
                logger.LogInformation("Starting batch {BatchIndex} of {BatchCount}: {FromDate} to {ToDate}.", batchIndex + 1, batches.Count, batch.From, batch.To);
                progress?.Report(new ExtractionProgress("Extracting date-scoped voucher batch", uniqueVoucherFingerprints.Count, totalMatched, ToPercent(batchIndex, batches.Count), false, batchIndex + 1, batches.Count, batch, totalMatched));

                var returned = await vouchers.GetVouchersAsync(request.CompanyContext.Company, batch.From, batch.To, cancellationToken);
                var batchUnique = 0;
                var batchMatched = 0;

                foreach (var voucher in returned)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (voucher.Date < batch.From || voucher.Date > batch.To)
                        throw new TallyProtocolException($"Tally returned voucher {VoucherIdentity(voucher)} dated {voucher.Date:yyyy-MM-dd} outside batch {batch.From:yyyy-MM-dd} to {batch.To:yyyy-MM-dd}. Extraction stopped to prevent incorrect data.");

                    var identity = VoucherIdentity(voucher);
                    var fingerprint = VoucherFingerprint(voucher);
                    if (uniqueVoucherFingerprints.TryGetValue(identity, out var existingFingerprint))
                    {
                        if (!string.Equals(existingFingerprint, fingerprint, StringComparison.Ordinal))
                            throw new ExtractionException($"Conflicting vouchers share identity '{identity}'. Extraction stopped to prevent data loss.");
                        continue;
                    }

                    uniqueVoucherFingerprints.Add(identity, fingerprint);
                    batchUnique++;
                    var voucherDebit = voucher.Entries.Where(x => x.Amount.Direction == DebitCredit.Debit).Sum(DebitContribution);
                    var voucherCredit = voucher.Entries.Where(x => x.Amount.Direction == DebitCredit.Credit).Sum(CreditContribution);
                    overallDebit += voucherDebit;
                    overallCredit += voucherCredit;
                    if (voucherDebit != voucherCredit) unbalancedVoucherIdentities.Add(identity);
                    for (var entryIndex = 0; entryIndex < voucher.Entries.Count; entryIndex++)
                    {
                        var entry = voucher.Entries[entryIndex];
                        if (!selectedLedgers.TryGetValue(CanonicalLedgerName(entry.LedgerName), out var ledger)) continue;
                        transactions[CanonicalLedgerName(ledger.Name)].Add(new LedgerTransaction(voucher, entry, entryIndex));
                        batchMatched++;
                    }
                }

                totalMatched += batchMatched;
                batchResults.Add(new ExtractionBatch(batch, returned.Count, batchUnique, batchMatched));
                logger.LogInformation("Completed batch {BatchIndex} of {BatchCount}: {ReturnedVoucherCount} returned, {UniqueVoucherCount} unique, {MatchedTransactionCount} ledger transactions.", batchIndex + 1, batches.Count, returned.Count, batchUnique, batchMatched);
                progress?.Report(new ExtractionProgress("Batch completed", uniqueVoucherFingerprints.Count, totalMatched, ToPercent(batchIndex + 1, batches.Count), false, batchIndex + 1, batches.Count, batch, totalMatched));
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Ledger export cancelled for {Company}.", request.CompanyContext.Company.Name);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Ledger export failed for {Company}.", request.CompanyContext.Company.Name);
            throw;
        }

        var ledgerResults = selectedLedgers
            .OrderBy(x => x.Value.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => BuildLedgerResult(x.Value, transactions[x.Key]))
            .ToList();
        var reconciliation = new ExtractionReconciliation(overallDebit, overallCredit, uniqueVoucherFingerprints.Count == 0 ? ReconciliationStatus.NotApplicable : unbalancedVoucherIdentities.Count == 0 ? ReconciliationStatus.Balanced : ReconciliationStatus.Unbalanced, unbalancedVoucherIdentities);
        var completedAt = DateTimeOffset.UtcNow;
        logger.LogInformation("Ledger export completed for {Company}; {VoucherCount} unique vouchers; {MatchedTransactionCount} ledger transactions; reconciliation {ReconciliationStatus}.", request.CompanyContext.Company.Name, uniqueVoucherFingerprints.Count, totalMatched, reconciliation.Status);
        progress?.Report(new ExtractionProgress("Ledger-wise extraction completed", uniqueVoucherFingerprints.Count, totalMatched, 100, true, batches.Count, batches.Count, request.DateRange, totalMatched));

        return new LedgerWiseExtractionResult(request, batchResults, ledgerResults, uniqueVoucherFingerprints.Count, totalMatched, startedAt, completedAt, reconciliation);
    }

    public static string CanonicalLedgerName(string name) => name.Trim().Normalize(NormalizationForm.FormKC);

    private static void ValidateRequest(LedgerWiseExtractionRequest request)
    {
        if (request.DateRange.From > request.DateRange.To) throw new ExtractionException("From Date must be on or before To Date.");
        if (request.CompanyContext.Company is null || string.IsNullOrWhiteSpace(request.CompanyContext.Company.Name)) throw new ExtractionException("The selected company is not available.");
        if (request.SelectedLedgers is null || request.SelectedLedgers.Count == 0) throw new ExtractionException("No selected ledgers were provided.");
        if (request.BatchDays < 1) throw new ExtractionException("Batch size must be at least one day.");
    }

    private static LedgerResult BuildLedgerResult(LedgerInfo ledger, List<LedgerTransaction> ledgerTransactions)
    {
        var ordered = ledgerTransactions
            .OrderBy(x => x.Voucher.Date)
            .ThenBy(x => x.Voucher.VoucherNumber ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(x => VoucherIdentity(x.Voucher), StringComparer.Ordinal)
            .ThenBy(x => x.LedgerEntryPosition)
            .ToList();
        return new LedgerResult(ledger, ordered,
            ordered.Where(x => x.Entry.Amount.Direction == DebitCredit.Debit).Sum(x => x.Entry.Amount.Value),
            ordered.Where(x => x.Entry.Amount.Direction == DebitCredit.Credit).Sum(x => x.Entry.Amount.Value));
    }

    private static int ToPercent(int completedBatches, int totalBatches) => totalBatches == 0 ? 100 : (int)Math.Floor(completedBatches * 95m / totalBatches);
    private static decimal DebitContribution(VoucherEntryInfo entry) => -(entry.SourceAmount ?? -entry.Amount.Value);
    private static decimal CreditContribution(VoucherEntryInfo entry) => entry.SourceAmount ?? entry.Amount.Value;

    private static string VoucherIdentity(VoucherInfo voucher)
    {
        if (!string.IsNullOrWhiteSpace(voucher.Guid)) return "GUID:" + voucher.Guid.Trim();
        if (!string.IsNullOrWhiteSpace(voucher.MasterId)) return "MASTERID:" + voucher.MasterId.Trim();
        return $"FALLBACK:{voucher.SourceId.Trim()}|{voucher.Date:yyyyMMdd}|{voucher.VoucherType}|{voucher.VoucherNumber}|{voucher.PartyLedgerName}|{voucher.ReferenceNumber}";
    }

    private static string VoucherFingerprint(VoucherInfo voucher) => string.Join("\u001F", [
        voucher.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture), voucher.VoucherType, voucher.VoucherNumber, voucher.Narration, voucher.PartyLedgerName, voucher.ReferenceNumber,
        string.Join("\u001E", voucher.Entries.Select(x => string.Join("\u001D", x.LedgerName, x.Amount.Direction, x.Amount.Value.ToString(CultureInfo.InvariantCulture), x.SourceAmount?.ToString(CultureInfo.InvariantCulture), x.Reference)))]);
}
