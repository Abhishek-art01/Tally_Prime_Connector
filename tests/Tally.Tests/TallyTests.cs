using TallyPrimeConnector.Contracts;
using TallyPrimeConnector.Tally;
namespace Tally.Tests;

public sealed class TallyTests
{
    private static readonly ConnectionProfile XmlProfile = new("p", "localhost", 9000, TallyProtocol.HttpXml, TallyConnectionMethod.XmlHttp);

    [Fact]
    public async Task Mock_provider_has_company()
    {
        var result = await new MockTallyCompanyProvider().GetCompaniesAsync(XmlProfile, CancellationToken.None);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task Invalid_port_is_rejected() => await Assert.ThrowsAsync<ArgumentException>(() => new TallyXmlHttpConnection(new TallyXmlHttpClient(), new TallyXmlResponseParser()).TestAsync(new("p", "localhost", 0, TallyProtocol.HttpXml, TallyConnectionMethod.XmlHttp), CancellationToken.None));

    [Fact]
    public void Voucher_collection_request_is_read_only_and_contains_the_documented_scope_variables()
    {
        var xml = TallyXmlRequestFactory.CreateVoucherCollectionRequest(new("company", "Aravali Dairy & Agro Products Pvt. Ltd (FY2026-27)"), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));

        Assert.Contains("<TALLYREQUEST>EXPORT</TALLYREQUEST>", xml, StringComparison.Ordinal);
        Assert.Contains("<TYPE>COLLECTION</TYPE>", xml, StringComparison.Ordinal);
        Assert.Contains("<ID>TPC Read Only Voucher Scope</ID>", xml, StringComparison.Ordinal);
        Assert.Contains("<SVCURRENTCOMPANY TYPE=\"String\">Aravali Dairy &amp; Agro Products Pvt. Ltd (FY2026-27)</SVCURRENTCOMPANY>", xml, StringComparison.Ordinal);
        Assert.Contains("<SVFROMDATE TYPE=\"Date\">20260901</SVFROMDATE>", xml, StringComparison.Ordinal);
        Assert.Contains("<SVTODATE TYPE=\"Date\">20260910</SVTODATE>", xml, StringComparison.Ordinal);
        Assert.Contains("<TYPE>Voucher</TYPE>", xml, StringComparison.Ordinal);
        Assert.Contains("<FETCH>LedgerEntries.*</FETCH>", xml, StringComparison.Ordinal);
        Assert.Contains("<FETCH>AllLedgerEntries.*</FETCH>", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("IMPORT", xml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<ACTION>", xml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_out_of_range_response()
    {
        var provider = new TallyXmlVoucherProvider(new StubXmlClient(VoucherXml("20260910")), new TallyXmlResponseParser(), XmlProfile);

        await Assert.ThrowsAsync<TallyProtocolException>(() => provider.GetVouchersAsync(new("id", "Company"), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), CancellationToken.None));
    }

    [Fact]
    public async Task Different_date_requests_do_not_silently_accept_the_same_out_of_period_response()
    {
        var firstClient = new StubXmlClient(VoucherXml("20260910"));
        var secondClient = new StubXmlClient(VoucherXml("20260910"));
        var company = new CompanyInfo("id", "Company");

        await Assert.ThrowsAsync<TallyProtocolException>(() => new TallyXmlVoucherProvider(firstClient, new TallyXmlResponseParser(), XmlProfile)
            .GetVouchersAsync(company, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), CancellationToken.None));
        await Assert.ThrowsAsync<TallyProtocolException>(() => new TallyXmlVoucherProvider(secondClient, new TallyXmlResponseParser(), XmlProfile)
            .GetVouchersAsync(company, new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 2), CancellationToken.None));

        Assert.Contains("<SVFROMDATE TYPE=\"Date\">20260901</SVFROMDATE>", firstClient.Request!, StringComparison.Ordinal);
        Assert.Contains("<SVFROMDATE TYPE=\"Date\">20260902</SVFROMDATE>", secondClient.Request!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Accepts_single_day_collection_response_and_preserves_ledger_direction()
    {
        var client = new StubXmlClient(VoucherXml("20260901", "Cash", "100.25", "Yes"));
        var provider = new TallyXmlVoucherProvider(client, new TallyXmlResponseParser(), XmlProfile);

        var vouchers = await provider.GetVouchersAsync(new("id", "Company"), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), CancellationToken.None);

        var voucher = Assert.Single(vouchers);
        Assert.Equal(new DateOnly(2026, 9, 1), voucher.Date);
        Assert.Equal("Cash", voucher.Entries.Single().LedgerName);
        Assert.Equal(100.25m, voucher.Entries.Single().Amount.Value);
        Assert.Equal(DebitCredit.Debit, voucher.Entries.Single().Amount.Direction);
        Assert.Contains("TPC Read Only Voucher Scope", client.Request!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Accepts_inclusive_multi_day_boundary_response()
    {
        var xml = Envelope(VoucherElement("20260901", "Cash", "1.00", "Yes") + VoucherElement("20260910", "Bank", "1.00", "No"));
        var provider = new TallyXmlVoucherProvider(new StubXmlClient(xml), new TallyXmlResponseParser(), XmlProfile);

        var vouchers = await provider.GetVouchersAsync(new("id", "Company"), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10), CancellationToken.None);

        Assert.Equal(2, vouchers.Count);
        Assert.Equal(new DateOnly(2026, 9, 1), vouchers.Min(x => x.Date));
        Assert.Equal(new DateOnly(2026, 9, 10), vouchers.Max(x => x.Date));
        Assert.Contains(vouchers.SelectMany(x => x.Entries), x => x.Amount.Direction == DebitCredit.Credit);
    }

    [Fact]
    public async Task Protocol_diagnostics_reports_date_scope_and_reconciles_signed_tally_amounts_with_decimal_arithmetic()
    {
        var xml = Envelope(
            "<VOUCHER><MASTERID>1</MASTERID><DATE>20260901</DATE><VOUCHERTYPENAME>Journal</VOUCHERTYPENAME><VOUCHERNUMBER>JV-1</VOUCHERNUMBER>" +
            "<ALLLEDGERENTRIES.LIST><LEDGERNAME>Expense</LEDGERNAME><ISDEEMEDPOSITIVE>Yes</ISDEEMEDPOSITIVE><AMOUNT>-100.10</AMOUNT></ALLLEDGERENTRIES.LIST>" +
            "<ALLLEDGERENTRIES.LIST><LEDGERNAME>Creditor</LEDGERNAME><ISDEEMEDPOSITIVE>No</ISDEEMEDPOSITIVE><AMOUNT>100.10</AMOUNT></ALLLEDGERENTRIES.LIST></VOUCHER>");
        var diagnostics = new TallyProtocolDiagnostics(new StubXmlClient(xml), new TallyXmlResponseParser(), XmlProfile);

        var result = await diagnostics.InspectVoucherScopeAsync(new("id", "Company"), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), CancellationToken.None);

        Assert.True(result.DateScopePass);
        Assert.Empty(result.UnbalancedVoucherNumbers);
        Assert.Equal(100.10m, result.DebitTotal);
        Assert.Equal(100.10m, result.CreditTotal);
        Assert.Equal("1", result.TallyStatus);
    }

    private static string VoucherXml(string date, string ledger = "Cash", string amount = "1.00", string deemedPositive = "Yes") => Envelope(VoucherElement(date, ledger, amount, deemedPositive));
    private static string VoucherElement(string date, string ledger, string amount, string deemedPositive) => $"<VOUCHER><MASTERID>{date}</MASTERID><DATE>{date}</DATE><VOUCHERTYPENAME>Journal</VOUCHERTYPENAME><VOUCHERNUMBER>{date}</VOUCHERNUMBER><ALLLEDGERENTRIES.LIST><LEDGERNAME>{ledger}</LEDGERNAME><ISDEEMEDPOSITIVE>{deemedPositive}</ISDEEMEDPOSITIVE><AMOUNT>{amount}</AMOUNT></ALLLEDGERENTRIES.LIST></VOUCHER>";
    private static string Envelope(string vouchers) => $"<ENVELOPE><HEADER><STATUS>1</STATUS></HEADER><BODY><DATA><COLLECTION>{vouchers}</COLLECTION></DATA></BODY></ENVELOPE>";

    private sealed class StubXmlClient(string xml) : ITallyXmlClient
    {
        public string? Request { get; private set; }
        public Task<string> SendAsync(string requestXml, ConnectionProfile profile, CancellationToken cancellationToken)
        {
            Request = requestXml;
            return Task.FromResult(xml);
        }
    }
}
