using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TallyPrimeConnector.Contracts;

namespace TallyPrimeConnector.Tally;

/// <summary>Transforms XML at the integration boundary into normalized contracts.</summary>
public sealed class TallyXmlResponseParser
{
    public IReadOnlyList<LedgerInfo> ParseLedgers(string xml)
    {
        var document = LoadSuccessfulEnvelope(xml);
        return document.Descendants().Where(x => IsNamed(x, "LEDGER") && Value(x, "NAME") is not null).Select(x => new LedgerInfo(
            Value(x, "MASTERID") ?? Value(x, "GUID") ?? Value(x, "NAME")!, Value(x, "NAME")!, Value(x, "PARENT"), ParseDecimal(Value(x, "OPENINGBALANCE")), ParseDecimal(Value(x, "CLOSINGBALANCE")), Value(x, "LEDGERTYPE"), Value(x, "NAME.LIST"), Value(x, "MAILINGNAME"), Value(x, "PARTYGSTIN"))).ToList();
    }

    public IReadOnlyList<GroupInfo> ParseGroups(string xml)
    {
        var document = LoadSuccessfulEnvelope(xml);
        return document.Descendants().Where(x => IsNamed(x, "GROUP") && Value(x, "NAME") is not null).Select(x => new GroupInfo(
            Value(x, "MASTERID") ?? Value(x, "GUID") ?? Value(x, "NAME")!, Value(x, "NAME")!, Value(x, "PARENT"), ParseBool(Value(x, "ISPRIMARY")), Value(x, "PRIMARYGROUP"))).ToList();
    }

    public IReadOnlyList<VoucherInfo> ParseVouchers(string xml)
    {
        var document = LoadSuccessfulEnvelope(xml);
        // Voucher exports can contain nested, non-transaction VOUCHER nodes inside component metadata.
        // A primary voucher is identified by its own direct accounting DATE method.
        return document.Descendants().Where(x => x.Name.LocalName.Equals("VOUCHER", StringComparison.OrdinalIgnoreCase) && Value(x, "DATE") is not null).Select(ParseVoucher).ToList();
    }

    public string? ParseCurrentCompanyName(string xml)
    {
        var document = LoadSuccessfulEnvelope(xml);
        var node = document.Descendants().FirstOrDefault(x => IsNamed(x, "SVCURRENTCOMPANY"));
        return node?.Value.Trim();
    }

    private static VoucherInfo ParseVoucher(XElement voucher)
    {
        var entries = voucher.Elements()
            .Where(x => (IsNamed(x, "LEDGERENTRIES.LIST") || IsNamed(x, "ALLLEDGERENTRIES.LIST")) && Value(x, "LEDGERNAME") is not null)
            .Concat(voucher.Elements()
                .Where(x => IsNamed(x, "INVENTORYENTRIES.LIST") || IsNamed(x, "ALLINVENTORYENTRIES.LIST"))
                .SelectMany(x => x.Elements().Where(y => IsNamed(y, "ACCOUNTINGALLOCATIONS.LIST") && Value(y, "LEDGERNAME") is not null)))
            .Select(ParseLedgerEntry)
            .ToList();
        var masterId = Value(voucher, "MASTERID");
        var guid = Value(voucher, "GUID");
        return new VoucherInfo(masterId ?? guid ?? Value(voucher, "VOUCHERNUMBER") ?? throw new TallyProtocolException("Voucher response contains no identifier."), Value(voucher, "VOUCHERNUMBER"), ParseDate(Value(voucher, "DATE")), Value(voucher, "VOUCHERTYPENAME") ?? Value(voucher, "VCHTYPE") ?? "Unknown", Value(voucher, "NARRATION"), entries, null, null, null, Value(voucher, "REFERENCE"), Value(voucher, "PARTYLEDGERNAME"), guid, masterId);
    }

    private static VoucherEntryInfo ParseLedgerEntry(XElement entry)
    {
        var amount = ParseDecimal(Value(entry, "AMOUNT")) ?? throw new TallyProtocolException($"Voucher ledger entry '{Value(entry, "LEDGERNAME") ?? "Unknown Ledger"}' has no valid amount.");
        var direction = string.Equals(Value(entry, "ISDEEMEDPOSITIVE"), "Yes", StringComparison.OrdinalIgnoreCase) ? DebitCredit.Debit : DebitCredit.Credit;
        var billAllocation = entry.Elements().FirstOrDefault(x => IsNamed(x, "BILLALLOCATIONS.LIST"));
        var reference = Value(entry, "BILLREF") ?? Value(billAllocation, "NAME");
        // Tally retains a signed AMOUNT. ISDEEMEDPOSITIVE identifies the debit/credit side;
        // SourceAmount preserves the original signed value for exact reconciliation, including round-off adjustments.
        return new VoucherEntryInfo(Value(entry, "MASTERID") ?? Value(entry, "LEDGERNAME") ?? $"voucher-entry-{entry.GetHashCode():X8}", Value(entry, "LEDGERNAME") ?? "Unknown Ledger", new TransactionAmount(Math.Abs(amount), direction), reference, amount);
    }

    private static XDocument LoadSuccessfulEnvelope(string xml)
    {
        try
        {
            // Some Tally exports contain XML 1.0-invalid control-character references in free-text fields.
            // Remove only those invalid characters before parsing; no accounting fields are altered.
            var sanitized = Regex.Replace(xml, @"&#(?:x0*(?:[0-8B-C-F]|1[0-9A-F])|0*[0-8])\s*;", string.Empty, RegexOptions.IgnoreCase);
            sanitized = new string(sanitized.Where(c => c is '\t' or '\n' or '\r' || c >= ' ').ToArray());
            var document = XDocument.Parse(sanitized, LoadOptions.None);
            var status = document.Descendants().FirstOrDefault(x => x.Name.LocalName.Equals("STATUS", StringComparison.OrdinalIgnoreCase))?.Value;
            if (status is "0" or "-1") throw new TallyProtocolException("TallyPrime rejected the read-only request: " + (Value(document.Root, "LINEERROR") ?? Value(document.Root, "DESC") ?? "unknown error"));
            return document;
        }
        catch (System.Xml.XmlException exception) { throw new TallyProtocolException("TallyPrime returned malformed XML.", exception); }
    }
    private static bool IsNamed(XElement source, string name) => source.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase);
    private static string? Value(XElement? source, string name)
    {
        if (source is null) return null;
        var child = source.Elements().FirstOrDefault(x => IsNamed(x, name))?.Value.Trim();
        return !string.IsNullOrWhiteSpace(child) ? child : source.Attributes().FirstOrDefault(x => x.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value.Trim();
    }
    private static decimal? ParseDecimal(string? value) => decimal.TryParse(value?.Replace(",", string.Empty), NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result) ? result : null;
    private static bool? ParseBool(string? value) => bool.TryParse(value, out var result) ? result : null;
    private static DateOnly ParseDate(string? value) => DateOnly.TryParseExact(value, ["yyyyMMdd", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : throw new TallyProtocolException("Voucher response contains no valid accounting date.");
}
