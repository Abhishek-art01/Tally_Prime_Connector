using TallyPrimeConnector.Contracts;
using TallyPrimeConnector.Core;

namespace TallyPrimeConnector.Tally.Mcp;

/// <summary>
/// Host-neutral tool adapter for a future local MCP server. It intentionally delegates to
/// application contracts and therefore cannot expose raw Tally memory/database access.
/// </summary>
public interface ITallyMcpTools
{
    Task<ConnectionTestResult> TestConnectionAsync(ConnectionProfile profile, CancellationToken cancellationToken);
    Task<IReadOnlyList<CompanyInfo>> ListCompaniesAsync(ConnectionProfile profile, CancellationToken cancellationToken);
}

public sealed class TallyMcpTools(IConnectionService connections, ICompanyService companies) : ITallyMcpTools
{
    public Task<ConnectionTestResult> TestConnectionAsync(ConnectionProfile profile, CancellationToken cancellationToken) => connections.TestAsync(profile, cancellationToken);
    public Task<IReadOnlyList<CompanyInfo>> ListCompaniesAsync(ConnectionProfile profile, CancellationToken cancellationToken) => companies.GetCompaniesAsync(profile, cancellationToken);
}
