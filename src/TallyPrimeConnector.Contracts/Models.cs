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
public sealed record VoucherInfo(string SourceId, string? VoucherNumber, DateOnly Date, string VoucherType, string? Narration, IReadOnlyList<VoucherEntryInfo> Entries, PartyInfo? Party = null, IReadOnlyList<InventoryEntryInfo>? InventoryEntries = null, IReadOnlyList<TaxEntryInfo>? TaxEntries = null, string? ReferenceNumber = null, string? PartyLedgerName = null);
public sealed record ExtractionMetadata(string CompanySourceId, DateOnly FromDate, DateOnly ToDate, DateTimeOffset RetrievedAt, string IntegrationMethod);
public sealed record DateRange(DateOnly From, DateOnly To)
{
    public static DateRange Create(DateOnly from, DateOnly to) => from <= to ? new(from, to) : throw new ArgumentException("From Date must be on or before To Date.");
}
public sealed record ExtractionScope(string? GroupName = null, IReadOnlyList<string>? LedgerNames = null);
public sealed record ExtractionProgress(string Stage, int RecordsFound, int RecordsProcessed, int Percent, bool IsCompleted = false);
public sealed record ExtractionRequest(CompanyContext CompanyContext, ExtractionScope Scope, DateRange DateRange);
public sealed record ExtractionResult(IReadOnlyList<VoucherInfo> Vouchers, ExtractionMetadata Metadata, int RecordsFound, int RecordsProcessed);
public sealed record ConnectionProfile(string Name, string Host, int Port, TallyProtocol Protocol, TallyConnectionMethod Method);
public enum TallyProtocol { HttpXml, Odbc, Tdl }
public enum TallyConnectionMethod { XmlHttp, Odbc, Tdl }
public sealed record ConnectionDiagnostic(string Check, bool Passed, string Message);
public sealed record ConnectionTestResult(bool IsSuccessful, IReadOnlyList<ConnectionDiagnostic> Diagnostics);
public enum JobStatus { Pending, Running, Completed, Failed, Cancelled }
public sealed record JobInfo(Guid JobId, string JobType, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, JobStatus Status, int Progress, string? Error, string? Input, string? Output);
