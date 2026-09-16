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
            case "live-sample":
                var client = new TallyXmlHttpClient();
                var parser = new TallyXmlResponseParser();
                var companies = await new TallyXmlCompanyProvider(client, parser).GetCompaniesAsync(profile, CancellationToken.None);
                foreach (var company in companies)
                {
                    Console.WriteLine($"Company: {company.Name}");
                    var collections = new TallyXmlCollectionProvider(client, parser, profile);
                    var groups = await collections.GetGroupsAsync(company, CancellationToken.None);
                    var ledgers = await collections.GetLedgersAsync(company, null, CancellationToken.None);
                    Console.WriteLine($"Groups: {groups.Count}; Ledgers: {ledgers.Count}");
                    foreach (var ledger in ledgers.Take(5)) Console.WriteLine($"Ledger: {ledger.Name} | Group: {ledger.GroupName ?? "(none)"}");
                    var vouchers = await new TallyXmlVoucherProvider(client, parser, profile).GetVouchersAsync(company, new DateOnly(2025, 4, 1), new DateOnly(2025, 4, 1), CancellationToken.None);
                    Console.WriteLine($"Vouchers returned for requested 2025-04-01: {vouchers.Count}");
                    foreach (var voucher in vouchers.Take(3)) Console.WriteLine($"Voucher: {voucher.Date:yyyy-MM-dd} | {voucher.VoucherType} | {voucher.VoucherNumber ?? "(none)"} | Entries: {voucher.Entries.Count}");
                }
                return 0;
            case "protocol-diagnostics":
                if (args.Length is < 3 || !DateOnly.TryParse(args[1], out var from) || !DateOnly.TryParse(args[2], out var to))
                {
                    Console.Error.WriteLine("Usage: protocol-diagnostics YYYY-MM-DD YYYY-MM-DD");
                    return 2;
                }
                var diagnosticClient = new TallyXmlHttpClient();
                var diagnosticParser = new TallyXmlResponseParser();
                var diagnosticCompanies = await new TallyXmlCompanyProvider(diagnosticClient, diagnosticParser).GetCompaniesAsync(profile, CancellationToken.None);
                var diagnosticCompany = diagnosticCompanies.FirstOrDefault();
                if (diagnosticCompany is null)
                {
                    Console.Error.WriteLine("No current Tally company was returned.");
                    return 1;
                }
                var scopeDiagnostic = await new TallyProtocolDiagnostics(diagnosticClient, diagnosticParser, profile).InspectVoucherScopeAsync(diagnosticCompany, from, to, CancellationToken.None);
                Console.WriteLine($"Endpoint: {scopeDiagnostic.Endpoint}");
                Console.WriteLine($"Mechanism: {scopeDiagnostic.ExtractionMechanism}");
                Console.WriteLine($"Tally status: {scopeDiagnostic.TallyStatus ?? "(unavailable)"}");
                Console.WriteLine($"Company: {scopeDiagnostic.Company}");
                Console.WriteLine($"Requested: {scopeDiagnostic.RequestedFrom:yyyy-MM-dd} to {scopeDiagnostic.RequestedTo:yyyy-MM-dd}");
                Console.WriteLine($"Vouchers: {scopeDiagnostic.VoucherCount}");
                Console.WriteLine($"Returned dates: {scopeDiagnostic.MinimumVoucherDate:yyyy-MM-dd} to {scopeDiagnostic.MaximumVoucherDate:yyyy-MM-dd}");
                Console.WriteLine($"Voucher numbers: {string.Join(", ", scopeDiagnostic.VoucherNumbers)}");
                Console.WriteLine($"Voucher types: {string.Join(", ", scopeDiagnostic.VoucherTypes)}");
                Console.WriteLine($"Ledgers: {string.Join(", ", scopeDiagnostic.LedgerNames)}");
                Console.WriteLine($"Debit: {scopeDiagnostic.DebitTotal:0.00}; Credit: {scopeDiagnostic.CreditTotal:0.00}");
                Console.WriteLine($"DATE FILTER: {(scopeDiagnostic.DateScopePass ? "PASS" : "FAIL")}");
                Console.WriteLine($"VOUCHER RECONCILIATION: {(scopeDiagnostic.UnbalancedVoucherNumbers.Count == 0 ? "PASS" : "FAIL: " + string.Join(", ", scopeDiagnostic.UnbalancedVoucherNumbers))}");
                if (args.Length > 3)
                {
                    var ledger = args[3];
                    var verifiedVouchers = await new TallyXmlVoucherProvider(diagnosticClient, diagnosticParser, profile).GetVouchersAsync(diagnosticCompany, from, to, CancellationToken.None);
                    var ledgerScoped = verifiedVouchers.Where(voucher => voucher.Entries.Any(entry => string.Equals(entry.LedgerName, ledger, StringComparison.OrdinalIgnoreCase))).ToList();
                    Console.WriteLine($"LEDGER FILTER: CLIENT-SIDE AFTER VERIFIED DATE SCOPE — {ledgerScoped.Count} voucher(s) for {ledger}");
                    Console.WriteLine($"Ledger voucher numbers: {string.Join(", ", ledgerScoped.Select(x => x.VoucherNumber ?? x.SourceId))}");
                }
                return scopeDiagnostic.DateScopePass && scopeDiagnostic.UnbalancedVoucherNumbers.Count == 0 ? 0 : 1;
            case "companies":
                foreach (var company in await new MockTallyCompanyProvider().GetCompaniesAsync(profile, CancellationToken.None)) Console.WriteLine(company.Name);
                return 0;
            case "connection-test":
                var result = await new TallyXmlHttpConnection(new TallyXmlHttpClient(), new TallyXmlResponseParser()).TestAsync(profile, CancellationToken.None);
                foreach (var diagnostic in result.Diagnostics) Console.WriteLine($"{diagnostic.Check}: {diagnostic.Message}");
                return result.IsSuccessful ? 0 : 1;
            default:
                Console.WriteLine("Tally Prime Connector CLI\nCommands: companies | connection-test | live-sample | protocol-diagnostics YYYY-MM-DD YYYY-MM-DD [ledger]");
                return 0;
        }
    }
}
