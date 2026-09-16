using TallyPrimeConnector.Contracts;
namespace TallyPrimeConnector.Core;
public sealed class CompanyService(ITallyCompanyProvider companies) : ICompanyService
{
    public Task<IReadOnlyList<CompanyInfo>> GetCompaniesAsync(ConnectionProfile profile, CancellationToken cancellationToken) => companies.GetCompaniesAsync(profile, cancellationToken);
}
