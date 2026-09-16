using TallyPrimeConnector.Contracts;
namespace TallyPrimeConnector.Core;

public interface ICompanyService { Task<IReadOnlyList<CompanyInfo>> GetCompaniesAsync(ConnectionProfile profile, CancellationToken cancellationToken); }
public interface IGroupService { Task<IReadOnlyList<GroupInfo>> GetGroupsAsync(CompanyInfo company, CancellationToken cancellationToken); }
public interface ILedgerService { Task<IReadOnlyList<LedgerInfo>> GetLedgersAsync(CompanyInfo company, string? group, CancellationToken cancellationToken); }
public interface IVoucherService { Task<IReadOnlyList<VoucherInfo>> GetVouchersAsync(CompanyInfo company, DateOnly from, DateOnly to, CancellationToken cancellationToken); }
public interface IExtractionService { Task<ExtractionResult> ExtractAsync(ExtractionRequest request, IProgress<ExtractionProgress>? progress, CancellationToken cancellationToken); }
public interface IExportService { }
public interface IProcessingService { }
public interface IConnectionService { Task<ConnectionTestResult> TestAsync(ConnectionProfile profile, CancellationToken cancellationToken); }

public sealed class ConnectionService(ITallyConnection connection) : IConnectionService
{ public Task<ConnectionTestResult> TestAsync(ConnectionProfile profile, CancellationToken cancellationToken) => connection.TestAsync(profile, cancellationToken); }

public sealed class GroupService(ITallyCollectionProvider collections) : IGroupService
{ public Task<IReadOnlyList<GroupInfo>> GetGroupsAsync(CompanyInfo company, CancellationToken cancellationToken) => collections.GetGroupsAsync(company, cancellationToken); }
public sealed class LedgerService(ITallyCollectionProvider collections) : ILedgerService
{ public Task<IReadOnlyList<LedgerInfo>> GetLedgersAsync(CompanyInfo company, string? group, CancellationToken cancellationToken) => collections.GetLedgersAsync(company, group, cancellationToken); }
public sealed class VoucherService(ITallyVoucherProvider vouchers) : IVoucherService
{ public Task<IReadOnlyList<VoucherInfo>> GetVouchersAsync(CompanyInfo company, DateOnly from, DateOnly to, CancellationToken cancellationToken) => vouchers.GetVouchersAsync(company, from, to, cancellationToken); }

public sealed class ExtractionService(IVoucherService vouchers) : IExtractionService
{
    public async Task<ExtractionResult> ExtractAsync(ExtractionRequest request, IProgress<ExtractionProgress>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(new("Connected", 0, 0, 10));
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new("Company selected", 0, 0, 25));
        var data = await vouchers.GetVouchersAsync(request.CompanyContext.Company, request.DateRange.From, request.DateRange.To, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var scoped = data.Where(v => request.Scope.LedgerNames is null || request.Scope.LedgerNames.Count == 0 || v.Entries.Any(e => request.Scope.LedgerNames.Contains(e.LedgerName, StringComparer.OrdinalIgnoreCase))).ToList();
        progress?.Report(new("Transactions extracted", scoped.Count, scoped.Count, 100, true));
        return new ExtractionResult(scoped, new ExtractionMetadata(request.CompanyContext.Company.SourceId, request.DateRange.From, request.DateRange.To, DateTimeOffset.UtcNow, request.CompanyContext.ConnectionProfile.Method.ToString()), scoped.Count, scoped.Count);
    }
}

public static class JobStateMachine
{
    public static bool CanTransition(JobStatus from, JobStatus to) => (from, to) switch
    { (JobStatus.Pending, JobStatus.Running or JobStatus.Cancelled) => true, (JobStatus.Running, JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled) => true, _ => false };
}
