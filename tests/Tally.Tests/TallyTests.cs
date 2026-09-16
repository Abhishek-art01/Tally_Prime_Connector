using TallyPrimeConnector.Contracts;
using TallyPrimeConnector.Tally;
namespace Tally.Tests;
public sealed class TallyTests { [Fact] public async Task Mock_provider_has_company() { var result = await new MockTallyCompanyProvider().GetCompaniesAsync(new("p", "localhost", 9000, TallyProtocol.HttpXml, TallyConnectionMethod.XmlHttp), CancellationToken.None); Assert.NotEmpty(result); } [Fact] public async Task Invalid_port_is_rejected() => await Assert.ThrowsAsync<ArgumentException>(() => new TallyXmlHttpConnection(new TallyXmlHttpClient(), new TallyXmlResponseParser()).TestAsync(new("p", "localhost", 0, TallyProtocol.HttpXml, TallyConnectionMethod.XmlHttp), CancellationToken.None)); }
