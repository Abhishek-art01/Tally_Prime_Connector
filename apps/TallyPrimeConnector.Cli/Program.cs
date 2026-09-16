using TallyPrimeConnector.Contracts;
using TallyPrimeConnector.Tally;

namespace TallyPrimeConnector.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var command = args.FirstOrDefault()?.ToLowerInvariant();
        var profile = new ConnectionProfile("CLI", "localhost", 9000, TallyProtocol.HttpXml, TallyConnectionMethod.XmlHttp);
        switch (command)
        {
            case "companies":
                foreach (var company in await new MockTallyCompanyProvider().GetCompaniesAsync(profile, CancellationToken.None)) Console.WriteLine(company.Name);
                return 0;
            case "connection-test":
                var result = await new TallyXmlHttpConnection(new TallyXmlHttpClient(), new TallyXmlResponseParser()).TestAsync(profile, CancellationToken.None);
                foreach (var diagnostic in result.Diagnostics) Console.WriteLine($"{diagnostic.Check}: {diagnostic.Message}");
                return result.IsSuccessful ? 0 : 1;
            default:
                Console.WriteLine("Tally Prime Connector CLI\nCommands: companies | connection-test");
                return 0;
        }
    }
}
