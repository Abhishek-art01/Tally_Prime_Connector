using TallyPrimeConnector.Contracts;
using TallyPrimeConnector.Tally;

namespace Tally.Tests;

public sealed class TallyXmlCollectionProviderTests
{
    private static readonly ConnectionProfile Profile = new("Test", "localhost", 9000, TallyProtocol.HttpXml, TallyConnectionMethod.XmlHttp);
    private static readonly CompanyInfo Company = new("company", "Demo Company");

    [Fact]
    public async Task Returns_no_ledgers_when_selected_group_has_no_exact_members()
    {
        const string xml = "<ENVELOPE><HEADER><STATUS>1</STATUS></HEADER><BODY><DATA><COLLECTION><LEDGER NAME=\"G Pay\"><MASTERID>1</MASTERID></LEDGER><LEDGER NAME=\"Cash\"><MASTERID>2</MASTERID></LEDGER></COLLECTION></DATA></BODY></ENVELOPE>";
        var provider = new TallyXmlCollectionProvider(new FixedXmlClient(xml), new TallyXmlResponseParser(), Profile);

        var ledgers = await provider.GetLedgersAsync(Company, "Bank Accounts", CancellationToken.None);

        Assert.Empty(ledgers);
    }

    [Fact]
    public async Task Applies_exact_group_filter_when_group_fields_are_available()
    {
        const string xml = "<ENVELOPE><HEADER><STATUS>1</STATUS></HEADER><BODY><DATA><COLLECTION><LEDGER NAME=\"Cash\"><PARENT>Cash-in-Hand</PARENT></LEDGER><LEDGER NAME=\"Bank\"><PARENT>Bank Accounts</PARENT></LEDGER></COLLECTION></DATA></BODY></ENVELOPE>";
        var provider = new TallyXmlCollectionProvider(new FixedXmlClient(xml), new TallyXmlResponseParser(), Profile);

        var ledgers = await provider.GetLedgersAsync(Company, "Bank Accounts", CancellationToken.None);

        var ledger = Assert.Single(ledgers);
        Assert.Equal("Bank", ledger.Name);
    }

    [Fact]
    public void Ledger_master_collection_is_read_only_and_fetches_parent_group()
    {
        var request = TallyXmlRequestFactory.CreateLedgerMasterCollectionRequest(Company);

        Assert.Contains("<TALLYREQUEST>EXPORT</TALLYREQUEST>", request);
        Assert.Contains("<TYPE>COLLECTION</TYPE>", request);
        Assert.Contains("<FETCH>Parent</FETCH>", request);
        Assert.DoesNotContain("<ACTION>", request);
    }

    private sealed class FixedXmlClient(string response) : ITallyXmlClient
    {
        public Task<string> SendAsync(string requestXml, ConnectionProfile profile, CancellationToken cancellationToken) => Task.FromResult(response);
    }
}
