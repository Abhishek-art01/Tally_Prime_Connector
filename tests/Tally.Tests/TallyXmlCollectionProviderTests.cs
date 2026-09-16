using TallyPrimeConnector.Contracts;
using TallyPrimeConnector.Tally;

namespace Tally.Tests;

public sealed class TallyXmlCollectionProviderTests
{
    private static readonly ConnectionProfile Profile = new("Test", "localhost", 9000, TallyProtocol.HttpXml, TallyConnectionMethod.XmlHttp);
    private static readonly CompanyInfo Company = new("company", "Demo Company");

    [Fact]
    public async Task Does_not_hide_ledgers_when_export_omits_group_fields()
    {
        const string xml = "<ENVELOPE><HEADER><STATUS>1</STATUS></HEADER><BODY><DATA><COLLECTION><LEDGER NAME=\"G Pay\"><MASTERID>1</MASTERID></LEDGER><LEDGER NAME=\"Cash\"><MASTERID>2</MASTERID></LEDGER></COLLECTION></DATA></BODY></ENVELOPE>";
        var provider = new TallyXmlCollectionProvider(new FixedXmlClient(xml), new TallyXmlResponseParser(), Profile);

        var ledgers = await provider.GetLedgersAsync(Company, "Bank Accounts", CancellationToken.None);

        Assert.Equal(["G Pay", "Cash"], ledgers.Select(x => x.Name));
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

    private sealed class FixedXmlClient(string response) : ITallyXmlClient
    {
        public Task<string> SendAsync(string requestXml, ConnectionProfile profile, CancellationToken cancellationToken) => Task.FromResult(response);
    }
}
