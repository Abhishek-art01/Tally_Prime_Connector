using System.Globalization;
using System.Xml.Linq;
using TallyPrimeConnector.Contracts;

namespace TallyPrimeConnector.Tally;

/// <summary>Transforms XML at the integration boundary into normalized contracts.</summary>
public sealed class TallyXmlResponseParser
{
    public IReadOnlyList<LedgerInfo> ParseLedgers(string xml)
    {
        var document = LoadSuccessfulEnvelope(xml);
        return document.Descendants().Where(x => x.Name.LocalName.Equals("LEDGER", StringComparison.OrdinalIgnoreCase)).Select(x => new LedgerInfo(
            Text(x, "MASTERID") ?? Text(x, "GUID") ?? Text(x, "NAME") ?? throw new TallyProtocolException("Ledger response contains no identifier."),
            Text(x, "NAME") ?? throw new TallyProtocolException("Ledger response contains no name."),
            Text(x, "PARENT"), ParseDecimal(Text(x, "OPENINGBALANCE")), ParseDecimal(Text(x, "CLOSINGBALANCE")), Text(x, "LEDGERTYPE"), Text(x, "NAME.LIST"), Text(x, "MAILINGNAME"), Text(x, "PARTYGSTIN"))).ToList();
    }

    public IReadOnlyList<GroupInfo> ParseGroups(string xml)
    {
        var document = LoadSuccessfulEnvelope(xml);
        return document.Descendants().Where(x => x.Name.LocalName.Equals("GROUP", StringComparison.OrdinalIgnoreCase)).Select(x => new GroupInfo(
            Text(x, "MASTERID") ?? Text(x, "GUID") ?? Text(x, "NAME") ?? throw new TallyProtocolException("Group response contains no identifier."),
            Text(x, "NAME") ?? throw new TallyProtocolException("Group response contains no name."), Text(x, "PARENT"), ParseBool(Text(x, "ISPRIMARY")), Text(x, "PRIMARYGROUP"))).ToList();
    }

    public IReadOnlyList<VoucherInfo> ParseVouchers(string xml)
    {
        var document = LoadSuccessfulEnvelope(xml);
        return document.Descendants().Where(x => x.Name.LocalName.Equals("VOUCHER", StringComparison.OrdinalIgnoreCase)).Select(ParseVoucher).ToList();
    }

    private static VoucherInfo ParseVoucher(XElement voucher)
    {
        var entries = voucher.Elements().Where(x => x.Name.LocalName.Equals("LEDGERENTRIES.LIST", StringComparison.OrdinalIgnoreCase)).Select(entry =>
        {
            var amount = ParseDecimal(Text(entry, "AMOUNT")) ?? throw new TallyProtocolException("Voucher ledger entry has no valid amount.");
            var direction = string.Equals(Text(entry, "ISDEEMEDPOSITIVE"), "Yes", StringComparison.OrdinalIgnoreCase) ? DebitCredit.Debit : DebitCredit.Credit;
            return new VoucherEntryInfo(Text(entry, "MASTERID") ?? Text(entry, "LEDGERNAME") ?? Guid.NewGuid().ToString("N"), Text(entry, "LEDGERNAME") ?? "Unknown Ledger", new TransactionAmount(Math.Abs(amount.Value), direction), Text(entry, "BILLREF"));
        }).ToList();
        return new VoucherInfo(Text(voucher, "MASTERID") ?? Text(voucher, "GUID") ?? Text(voucher, "VOUCHERNUMBER") ?? throw new TallyProtocolException("Voucher response contains no identifier."), Text(voucher, "VOUCHERNUMBER"), ParseDate(Text(voucher, "DATE")), Text(voucher, "VOUCHERTYPENAME") ?? "Unknown", Text(voucher, "NARRATION"), entries, null, null, null, Text(voucher, "REFERENCE"), Text(voucher, "PARTYLEDGERNAME"));
    }

    private static XDocument LoadSuccessfulEnvelope(string xml)
    {
        try
        {
            var document = XDocument.Parse(xml, LoadOptions.None);
            var status = document.Descendants().FirstOrDefault(x => x.Name.LocalName.Equals("STATUS", StringComparison.OrdinalIgnoreCase))?.Value;
            if (status is "0" or "-1") throw new TallyProtocolException("TallyPrime rejected the read-only request: " + (Text(document.Root, "LINEERROR") ?? Text(document.Root, "DESC") ?? "unknown error"));
            return document;
        }
        catch (System.Xml.XmlException exception) { throw new TallyProtocolException("TallyPrime returned malformed XML.", exception); }
    }
    private static string? Text(XElement? source, string name) => source?.Elements().FirstOrDefault(x => x.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value.Trim();
    private static decimal? ParseDecimal(string? value) => decimal.TryParse(value?.Replace(",", string.Empty), NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result) ? result : null;
    private static bool? ParseBool(string? value) => bool.TryParse(value, out var result) ? result : null;
    private static DateOnly ParseDate(string? value) => DateOnly.TryParseExact(value, ["yyyyMMdd", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : throw new TallyProtocolException("Voucher response contains no valid accounting date.");
}
