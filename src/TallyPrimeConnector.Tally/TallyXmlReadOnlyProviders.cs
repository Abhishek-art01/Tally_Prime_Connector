using TallyPrimeConnector.Contracts;

namespace TallyPrimeConnector.Tally;

/// <summary>Real XML/HTTP provider for request formats verified in official TallyHelp documentation.</summary>
public sealed class TallyXmlCollectionProvider(ITallyXmlClient client, TallyXmlResponseParser parser, ConnectionProfile profile) : ITallyCollectionProvider
{
    public async Task<IReadOnlyList<GroupInfo>> GetGroupsAsync(CompanyInfo company, CancellationToken cancellationToken)
    {
        var response = await client.SendAsync(TallyXmlRequestFactory.CreateGroupListRequest(company), profile, cancellationToken);
        return parser.ParseGroups(response);
    }

    public async Task<IReadOnlyList<LedgerInfo>> GetLedgersAsync(CompanyInfo company, string? groupName, CancellationToken cancellationToken)
    {
        var ledgers = await client.SendAsync(TallyXmlRequestFactory.CreateLedgerListRequest(company), profile, cancellationToken);
        var result = parser.ParseLedgers(ledgers);

        // The installed Tally collection can return ledgers without PARENT/group fields.
        // In that response shape, applying a group filter would incorrectly hide every ledger.
        // Group selection remains workflow metadata unless the returned ledger masters provide
        // enough identity data to perform an exact, client-side group comparison.
        if (string.IsNullOrWhiteSpace(groupName) || result.All(x => string.IsNullOrWhiteSpace(x.GroupName)))
        {
            return result;
        }

        return result.Where(x => string.Equals(x.GroupName, groupName, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}

public sealed class TallyXmlCompanyProvider(ITallyXmlClient client, TallyXmlResponseParser parser) : ITallyCompanyProvider
{
    public async Task<IReadOnlyList<CompanyInfo>> GetCompaniesAsync(ConnectionProfile profile, CancellationToken cancellationToken)
    {
        var response = await client.SendAsync(TallyXmlRequestFactory.CreateGroupListRequest(), profile, cancellationToken);
        var name = parser.ParseCurrentCompanyName(response);
        return string.IsNullOrWhiteSpace(name) ? [] : [new CompanyInfo(name, name)];
    }
}

public sealed class TallyXmlVoucherProvider(ITallyXmlClient client, TallyXmlResponseParser parser, ConnectionProfile profile) : ITallyVoucherProvider
{
    public async Task<IReadOnlyList<VoucherInfo>> GetVouchersAsync(CompanyInfo company, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (from > to) throw new ArgumentException("From Date must be on or before To Date.");
        var response = await client.SendAsync(TallyXmlRequestFactory.CreateVoucherCollectionRequest(company, from, to), profile, cancellationToken);
        var vouchers = parser.ParseVouchers(response);
        // Never silently accept a collection response whose server-side date scope is wrong.
        // Client-side ledger selection is permitted only after this validation succeeds.
        var outOfRange = vouchers.FirstOrDefault(v => v.Date < from || v.Date > to);
        if (outOfRange is not null)
            throw new TallyProtocolException($"TallyPrime Voucher collection response was not date-scoped. Requested {from:yyyy-MM-dd} to {to:yyyy-MM-dd}, but voucher {outOfRange.VoucherNumber ?? outOfRange.SourceId} has date {outOfRange.Date:yyyy-MM-dd}.");
        return vouchers;
    }
}
