using TallyPrimeConnector.Contracts;

namespace TallyPrimeConnector.Tally;

/// <summary>Real XML/HTTP provider for request formats verified in official TallyHelp documentation.</summary>
public sealed class TallyXmlCollectionProvider(ITallyXmlClient client, TallyXmlResponseParser parser, ConnectionProfile profile) : ITallyCollectionProvider
{
    public Task<IReadOnlyList<GroupInfo>> GetGroupsAsync(CompanyInfo company, CancellationToken cancellationToken) =>
        throw new TallyProtocolException("TODO: VERIFY WITH TALLYPRIME — a read-only group collection request format has not been verified for this connector.");

    public async Task<IReadOnlyList<LedgerInfo>> GetLedgersAsync(CompanyInfo company, string? groupName, CancellationToken cancellationToken)
    {
        var ledgers = await client.SendAsync(TallyXmlRequestFactory.CreateLedgerListRequest(company), profile, cancellationToken);
        var result = parser.ParseLedgers(ledgers);
        return groupName is null ? result : result.Where(x => string.Equals(x.GroupName, groupName, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}

public sealed class TallyXmlCompanyProvider : ITallyCompanyProvider
{
    public Task<IReadOnlyList<CompanyInfo>> GetCompaniesAsync(ConnectionProfile profile, CancellationToken cancellationToken) =>
        throw new TallyProtocolException("TODO: VERIFY WITH TALLYPRIME — company discovery request format has not been verified. Tally must not be queried with a fabricated request.");
}

public sealed class TallyXmlVoucherProvider : ITallyVoucherProvider
{
    public Task<IReadOnlyList<VoucherInfo>> GetVouchersAsync(CompanyInfo company, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        throw new TallyProtocolException("TODO: VERIFY WITH TALLYPRIME — voucher collection/report and date filter request format have not been verified.");
}
