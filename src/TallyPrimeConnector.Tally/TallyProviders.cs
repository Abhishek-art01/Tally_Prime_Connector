using TallyPrimeConnector.Contracts;
namespace TallyPrimeConnector.Tally;

public sealed class MockTallyCompanyProvider : ITallyCompanyProvider
{ public Task<IReadOnlyList<CompanyInfo>> GetCompaniesAsync(ConnectionProfile profile, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CompanyInfo>>([new("mock-company", "Demo Tally Company", new DateTime(2026, 4, 1))]); }

public sealed class MockTallyCollectionProvider : ITallyCollectionProvider
{
    public Task<IReadOnlyList<GroupInfo>> GetGroupsAsync(CompanyInfo company, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GroupInfo>>([new("assets", "Current Assets", null, true, "Primary"), new("sundry-debtors", "Sundry Debtors", "Current Assets", false, "Secondary")]);
    public Task<IReadOnlyList<LedgerInfo>> GetLedgersAsync(CompanyInfo company, string? groupName, CancellationToken cancellationToken) { var all = new List<LedgerInfo> { new("acme", "Acme Trading", "Sundry Debtors", 1250.50m, 3875.25m, "Party", "Acme", null, "27ABCDE1234F1Z5"), new("cash", "Cash", "Cash-in-Hand", 10000m, 12500m, "Cash") }; return Task.FromResult<IReadOnlyList<LedgerInfo>>(all.Where(x => groupName is null || x.GroupName == groupName).ToList()); }
}
public sealed class MockTallyVoucherProvider : ITallyVoucherProvider
{ public Task<IReadOnlyList<VoucherInfo>> GetVouchersAsync(CompanyInfo company, DateOnly from, DateOnly to, CancellationToken cancellationToken) { var sample = new VoucherInfo("voucher-001", "RCPT-001", new DateOnly(2026, 4, 1), "Receipt", "Synthetic sample only", [new("entry-1", "Cash", new(2500.25m, DebitCredit.Debit)), new("entry-2", "Acme Trading", new(2500.25m, DebitCredit.Credit))], new PartyInfo("Acme Trading"), ReferenceNumber: "REF-001", PartyLedgerName: "Acme Trading"); return Task.FromResult<IReadOnlyList<VoucherInfo>>(sample.Date >= from && sample.Date <= to ? [sample] : []); } }

public sealed class TallyXmlHttpConnection(ITallyXmlClient client, TallyXmlResponseParser parser) : ITallyConnection
{
    public async Task<ConnectionTestResult> TestAsync(ConnectionProfile profile, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(profile.Host) || profile.Port is < 1 or > 65535) throw new ArgumentException("Host and port must be valid.", nameof(profile));
        try { var response = await client.SendAsync(TallyXmlRequestFactory.CreateLedgerListRequest(), profile, cancellationToken); parser.ParseLedgers(response); return new ConnectionTestResult(true, [new("HTTP/XML", true, "Read-only List of Ledgers request returned valid XML."), new("Connection response", true, "TallyPrime response parsed successfully.")]); }
        catch (Exception exception) when (exception is not OperationCanceledException) { return new ConnectionTestResult(false, [new("HTTP/XML", false, "Unable to complete a read-only XML request."), new("Connection response", false, exception.Message)]); }
    }
}
