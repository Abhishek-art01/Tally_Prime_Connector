namespace TallyPrimeConnector.Contracts;

public sealed record CompanyInfo(string SourceId, string Name, DateTime? FinancialYearStart = null, string? Address = null);
public sealed record CompanyContext(CompanyInfo Company, ConnectionProfile ConnectionProfile, bool IsSelected = true);
public sealed record GroupInfo(string SourceId, string Name, string? ParentName = null, bool? IsPrimary = null, string? Classification = null);
public sealed record LedgerInfo(string SourceId, string Name, string? GroupName = null, decimal? OpeningBalance = null, decimal? ClosingBalance = null, string? Category = null, string? Alias = null, string? MailingAddress = null, string? TaxRegistrationNumber = null);
public sealed record PartyInfo(string? Name, string? TaxRegistrationNumber = null, string? Address = null);
public sealed record TransactionAmount(decimal Value, DebitCredit Direction, string? Currency = null);
public enum DebitCredit { Debit, Credit }
public sealed record VoucherEntryInfo(string SourceId, string LedgerName, TransactionAmount Amount, string? Reference = null, decimal? SourceAmount = null);
public sealed record InventoryEntryInfo(string? StockItemName, decimal? Quantity, decimal? Rate, decimal? Amount);
public sealed record TaxEntryInfo(string? TaxType, decimal Amount, decimal? Rate = null);
public sealed record VoucherInfo(string SourceId, string? VoucherNumber, DateOnly Date, string VoucherType, string? Narration, IReadOnlyList<VoucherEntryInfo> Entries, PartyInfo? Party = null, IReadOnlyList<InventoryEntryInfo>? InventoryEntries = null, IReadOnlyList<TaxEntryInfo>? TaxEntries = null, string? ReferenceNumber = null, string? PartyLedgerName = null, string? Guid = null, string? MasterId = null);
public sealed record ExtractionMetadata(string CompanySourceId, DateOnly FromDate, DateOnly ToDate, DateTimeOffset RetrievedAt, string IntegrationMethod);
public sealed record DateRange(DateOnly From, DateOnly To)
{
    public static DateRange Create(DateOnly from, DateOnly to) => from <= to ? new(from, to) : throw new ArgumentException("From Date must be on or before To Date.");
}
public sealed record ExtractionScope(string? GroupName = null, IReadOnlyList<string>? LedgerNames = null);
public sealed record ExtractionProgress(string Stage, int RecordsFound, int RecordsProcessed, int Percent, bool IsCompleted = false, int CurrentBatch = 0, int TotalBatches = 0, DateRange? CurrentDateRange = null, int MatchedTransactions = 0);
public sealed record ExtractionRequest(CompanyContext CompanyContext, ExtractionScope Scope, DateRange DateRange);
public sealed record ExtractionResult(IReadOnlyList<VoucherInfo> Vouchers, ExtractionMetadata Metadata, int RecordsFound, int RecordsProcessed);
public sealed record LedgerWiseExtractionRequest(CompanyContext CompanyContext, DateRange DateRange, string? SelectedGroup, IReadOnlyList<LedgerInfo> SelectedLedgers, int BatchDays, string OutputPath)
{
    public const int DefaultBatchDays = 31;

    public static LedgerWiseExtractionRequest Create(CompanyContext companyContext, DateRange dateRange, string? selectedGroup, IReadOnlyList<LedgerInfo>? selectedLedgers, string outputPath, int batchDays = DefaultBatchDays)
    {
        if (companyContext.Company is null || string.IsNullOrWhiteSpace(companyContext.Company.Name)) throw new ExtractionException("A company context is required.");
        if (selectedLedgers is null || selectedLedgers.Count == 0) throw new ExtractionException("No selected ledgers were provided.");
        if (selectedLedgers.Any(x => string.IsNullOrWhiteSpace(x.Name))) throw new ExtractionException("Each selected ledger must have a name.");
        if (batchDays < 1) throw new ExtractionException("Batch size must be at least one day.");
        if (string.IsNullOrWhiteSpace(outputPath)) throw new ExportException("An Excel output path is required.");

        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) throw new ExportException("The Excel output directory does not exist.");
        if (!string.Equals(Path.GetExtension(fullPath), ".xlsx", StringComparison.OrdinalIgnoreCase)) throw new ExportException("The Excel output path must use the .xlsx extension.");

        return new LedgerWiseExtractionRequest(companyContext, dateRange, selectedGroup, selectedLedgers, batchDays, fullPath);
    }
}
public sealed record ExtractionBatch(DateRange DateRange, int VouchersReceived, int UniqueVouchers, int MatchedTransactions);
public sealed record LedgerTransaction(VoucherInfo Voucher, VoucherEntryInfo Entry, int LedgerEntryPosition);
public sealed record LedgerResult(LedgerInfo Ledger, IReadOnlyList<LedgerTransaction> Transactions, decimal DebitTotal, decimal CreditTotal)
{
    public int TransactionCount => Transactions.Count;
}
public enum ReconciliationStatus { Balanced, Unbalanced, NotApplicable }
public sealed record ExtractionReconciliation(decimal DebitTotal, decimal CreditTotal, ReconciliationStatus Status, IReadOnlyList<string> UnbalancedVoucherIdentities);
public sealed record LedgerWiseExtractionResult(LedgerWiseExtractionRequest Request, IReadOnlyList<ExtractionBatch> Batches, IReadOnlyList<LedgerResult> Ledgers, int TotalVoucherCount, int TotalMatchedTransactionCount, DateTimeOffset StartedAt, DateTimeOffset CompletedAt, ExtractionReconciliation Reconciliation);
public sealed record LedgerWiseExportResult(LedgerWiseExtractionResult Extraction, string OutputPath);
public sealed record ConnectionProfile(string Name, string Host, int Port, TallyProtocol Protocol, TallyConnectionMethod Method);
public enum TallyProtocol { HttpXml, Odbc, Tdl }
public enum TallyConnectionMethod { XmlHttp, Odbc, Tdl }
public sealed record ConnectionDiagnostic(string Check, bool Passed, string Message);
public sealed record ConnectionTestResult(bool IsSuccessful, IReadOnlyList<ConnectionDiagnostic> Diagnostics);
public enum JobStatus { Pending, Running, Completed, Failed, Cancelled }
public sealed record JobInfo(Guid JobId, string JobType, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, JobStatus Status, int Progress, string? Error, string? Input, string? Output);
