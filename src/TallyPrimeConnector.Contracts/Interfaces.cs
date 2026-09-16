namespace TallyPrimeConnector.Contracts;

public interface ITallyConnection { Task<ConnectionTestResult> TestAsync(ConnectionProfile profile, CancellationToken cancellationToken); }
public interface ITallyXmlClient { Task<string> SendAsync(string requestXml, ConnectionProfile profile, CancellationToken cancellationToken); }
public interface ITallyOdbcClient { Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(string query, ConnectionProfile profile, CancellationToken cancellationToken); }
public interface ITallyTdlClient { Task<bool> IsAvailableAsync(ConnectionProfile profile, CancellationToken cancellationToken); }
public interface ITallyCompanyProvider { Task<IReadOnlyList<CompanyInfo>> GetCompaniesAsync(ConnectionProfile profile, CancellationToken cancellationToken); }
public interface ITallyCollectionProvider { Task<IReadOnlyList<GroupInfo>> GetGroupsAsync(CompanyInfo company, CancellationToken cancellationToken); Task<IReadOnlyList<LedgerInfo>> GetLedgersAsync(CompanyInfo company, string? groupName, CancellationToken cancellationToken); }
public interface ITallyVoucherProvider { Task<IReadOnlyList<VoucherInfo>> GetVouchersAsync(CompanyInfo company, DateOnly from, DateOnly to, CancellationToken cancellationToken); }
public interface IExtractionProgressReporter { void Report(ExtractionProgress progress); }
public interface IExcelExporter { Task<string> ExportAsync(IEnumerable<VoucherInfo> vouchers, string outputPath, CancellationToken cancellationToken); }
public interface ILedgerWorkbookExporter : IExcelExporter { Task<string> ExportAsync(LedgerWiseExtractionResult extraction, CancellationToken cancellationToken); }
public interface IUpdateService { Task<bool> IsUpdateAvailableAsync(CancellationToken cancellationToken); }
