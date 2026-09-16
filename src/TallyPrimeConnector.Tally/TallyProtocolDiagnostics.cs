using System.Xml.Linq;
using TallyPrimeConnector.Contracts;

namespace TallyPrimeConnector.Tally;

public sealed record TallyVoucherScopeDiagnostic(
    string Endpoint,
    string ExtractionMechanism,
    string? TallyStatus,
    string Company,
    DateOnly RequestedFrom,
    DateOnly RequestedTo,
    int VoucherCount,
    DateOnly? MinimumVoucherDate,
    DateOnly? MaximumVoucherDate,
    IReadOnlyList<string> VoucherNumbers,
    IReadOnlyList<string> VoucherTypes,
    IReadOnlyList<string> LedgerNames,
    decimal DebitTotal,
    decimal CreditTotal,
    bool DateScopePass,
    IReadOnlyList<string> UnbalancedVoucherNumbers);

/// <summary>Runs read-only, fail-closed protocol diagnostics using the same collection and normalizer as extraction.</summary>
public sealed class TallyProtocolDiagnostics(ITallyXmlClient client, TallyXmlResponseParser parser, ConnectionProfile profile)
{
    public async Task<TallyVoucherScopeDiagnostic> InspectVoucherScopeAsync(CompanyInfo company, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (from > to) throw new ArgumentException("From Date must be on or before To Date.");

        var request = TallyXmlRequestFactory.CreateVoucherCollectionRequest(company, from, to);
        var response = await client.SendAsync(request, profile, cancellationToken);
        var vouchers = parser.ParseVouchers(response);
        var outOfRange = vouchers.Any(voucher => voucher.Date < from || voucher.Date > to);
        var debit = vouchers.SelectMany(x => x.Entries).Where(x => x.Amount.Direction == DebitCredit.Debit).Sum(DebitContribution);
        var credit = vouchers.SelectMany(x => x.Entries).Where(x => x.Amount.Direction == DebitCredit.Credit).Sum(CreditContribution);
        var unbalanced = vouchers.Where(IsUnbalanced).Select(x => x.VoucherNumber ?? x.SourceId).ToList();

        return new TallyVoucherScopeDiagnostic(
            $"http://{profile.Host}:{profile.Port}",
            "EXPORT / COLLECTION / ephemeral read-only Voucher TDL collection",
            ReadStatus(response),
            company.Name,
            from,
            to,
            vouchers.Count,
            vouchers.Count == 0 ? null : vouchers.Min(x => x.Date),
            vouchers.Count == 0 ? null : vouchers.Max(x => x.Date),
            vouchers.Select(x => x.VoucherNumber ?? x.SourceId).ToList(),
            vouchers.Select(x => x.VoucherType).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList(),
            vouchers.SelectMany(x => x.Entries).Select(x => x.LedgerName).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList(),
            debit,
            credit,
            !outOfRange,
            unbalanced);
    }

    private static bool IsUnbalanced(VoucherInfo voucher)
    {
        var debit = voucher.Entries.Where(x => x.Amount.Direction == DebitCredit.Debit).Sum(DebitContribution);
        var credit = voucher.Entries.Where(x => x.Amount.Direction == DebitCredit.Credit).Sum(CreditContribution);
        return debit != credit;
    }

    private static decimal DebitContribution(VoucherEntryInfo entry) => -(entry.SourceAmount ?? -entry.Amount.Value);
    private static decimal CreditContribution(VoucherEntryInfo entry) => entry.SourceAmount ?? entry.Amount.Value;

    private static string? ReadStatus(string response)
    {
        try
        {
            var sanitized = System.Text.RegularExpressions.Regex.Replace(response, @"&#(?:x0*(?:[0-8B-C-F]|1[0-9A-F])|0*[0-8])\s*;", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return XDocument.Parse(sanitized).Descendants().FirstOrDefault(x => x.Name.LocalName.Equals("STATUS", StringComparison.OrdinalIgnoreCase))?.Value.Trim();
        }
        catch { return null; }
    }
}
