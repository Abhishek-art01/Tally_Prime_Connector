using TallyPrimeConnector.Contracts;
using TallyPrimeConnector.Tally;
namespace Tally.Tests;
public sealed class XmlParsingTests
{
    private const string Xml = "<ENVELOPE><HEADER><STATUS>1</STATUS></HEADER><BODY><DATA><GROUP><MASTERID>g1</MASTERID><NAME>Current Assets</NAME><PARENT></PARENT><ISPRIMARY>True</ISPRIMARY></GROUP><LEDGER><MASTERID>l1</MASTERID><NAME>Acme Trading</NAME><PARENT>Sundry Debtors</PARENT><OPENINGBALANCE>1,250.50</OPENINGBALANCE><CLOSINGBALANCE>2,500.75</CLOSINGBALANCE></LEDGER><VOUCHER><MASTERID>v1</MASTERID><VOUCHERNUMBER>1</VOUCHERNUMBER><DATE>20260401</DATE><VOUCHERTYPENAME>Receipt</VOUCHERTYPENAME><NARRATION>Test</NARRATION><LEDGERENTRIES.LIST><LEDGERNAME>Cash</LEDGERNAME><ISDEEMEDPOSITIVE>Yes</ISDEEMEDPOSITIVE><AMOUNT>100.25</AMOUNT></LEDGERENTRIES.LIST></VOUCHER></DATA></BODY></ENVELOPE>";
    [Fact] public void Parses_group_hierarchy() { var result = new TallyXmlResponseParser().ParseGroups(Xml); Assert.Equal("Current Assets", result[0].Name); Assert.True(result[0].IsPrimary); }
    [Fact] public void Parses_ledger_decimal_balances() { var result = new TallyXmlResponseParser().ParseLedgers(Xml); Assert.Equal(1250.50m, result[0].OpeningBalance); Assert.Equal("Sundry Debtors", result[0].GroupName); }
    [Fact] public void Parses_voucher_date_and_direction() { var result = new TallyXmlResponseParser().ParseVouchers(Xml); Assert.Equal(new DateOnly(2026, 4, 1), result[0].Date); Assert.Equal(DebitCredit.Debit, result[0].Entries[0].Amount.Direction); }
    [Fact] public void Rejects_malformed_xml() => Assert.Throws<TallyProtocolException>(() => new TallyXmlResponseParser().ParseGroups("<not-xml"));
    [Fact] public void Ignores_summary_ledger_count_nodes() { var result = new TallyXmlResponseParser().ParseLedgers("<ENVELOPE><HEADER><STATUS>1</STATUS></HEADER><BODY><DESC><CMPINFO><LEDGER>0</LEDGER></CMPINFO></DESC><DATA><COLLECTION /></DATA></BODY></ENVELOPE>"); Assert.Empty(result); }
    [Fact] public void Parses_live_attribute_based_master_fields() { var parser = new TallyXmlResponseParser(); var ledger = parser.ParseLedgers("<ENVELOPE><HEADER><STATUS>1</STATUS></HEADER><BODY><DATA><COLLECTION><LEDGER NAME=\"Cash\"><PARENT>Cash-in-Hand</PARENT><GUID>g1</GUID></LEDGER></COLLECTION></DATA></BODY></ENVELOPE>"); var group = parser.ParseGroups("<ENVELOPE><HEADER><STATUS>1</STATUS></HEADER><BODY><DATA><COLLECTION><GROUP NAME=\"Bank Accounts\"><PARENT>Current Assets</PARENT><GUID>g2</GUID></GROUP></COLLECTION></DATA></BODY></ENVELOPE>"); Assert.Equal("Cash", ledger[0].Name); Assert.Equal("g1", ledger[0].SourceId); Assert.Equal("Current Assets", group[0].ParentName); }
    [Fact] public void Parses_current_company_context() { Assert.Equal("Demo Company", new TallyXmlResponseParser().ParseCurrentCompanyName("<ENVELOPE><HEADER><STATUS>1</STATUS></HEADER><BODY><DESC><SVCURRENTCOMPANY>Demo Company</SVCURRENTCOMPANY></DESC></BODY></ENVELOPE>")); }
    [Fact] public void Sanitizes_tally_control_reference_without_changing_amount_or_date()
    {
        const string xml = "<ENVELOPE><HEADER><STATUS>1</STATUS></HEADER><BODY><DATA><TALLYMESSAGE><VOUCHER REMOTEID=\"r1\"><DATE>20260901</DATE><VOUCHERTYPENAME>Journal</VOUCHERTYPENAME><VOUCHERNUMBER>280</VOUCHERNUMBER><NARRATION>&#4; Not Applicable</NARRATION><ALLLEDGERENTRIES.LIST><LEDGERNAME>SALE</LEDGERNAME><ISDEEMEDPOSITIVE>No</ISDEEMEDPOSITIVE><AMOUNT>3333.33</AMOUNT></ALLLEDGERENTRIES.LIST></VOUCHER></TALLYMESSAGE></DATA></BODY></ENVELOPE>";
        var voucher = new TallyXmlResponseParser().ParseVouchers(xml).Single();
        Assert.Equal(new DateOnly(2026, 9, 1), voucher.Date);
        Assert.Equal(3333.33m, voucher.Entries.Single().Amount.Value);
        Assert.Equal(3333.33m, voucher.Entries.Single().SourceAmount);
        Assert.Equal(DebitCredit.Credit, voucher.Entries.Single().Amount.Direction);
    }

    [Fact] public void Ignores_nested_non_transaction_voucher_nodes()
    {
        const string xml = "<ENVELOPE><HEADER><STATUS>1</STATUS></HEADER><BODY><DATA><COLLECTION><VOUCHER><MASTERID>primary</MASTERID><DATE>20260901</DATE><VOUCHERNUMBER>1</VOUCHERNUMBER><VOUCHERTYPENAME>Journal</VOUCHERTYPENAME><VOUCHER><NAME>metadata only</NAME></VOUCHER></VOUCHER></COLLECTION></DATA></BODY></ENVELOPE>";
        var voucher = Assert.Single(new TallyXmlResponseParser().ParseVouchers(xml));
        Assert.Equal("primary", voucher.SourceId);
    }

    [Fact] public void Parses_inventory_accounting_allocations_as_ledger_entries()
    {
        const string xml = "<ENVELOPE><HEADER><STATUS>1</STATUS></HEADER><BODY><DATA><VOUCHER><MASTERID>sale</MASTERID><DATE>20260901</DATE><VOUCHERNUMBER>S-1</VOUCHERNUMBER><VOUCHERTYPENAME>Sales</VOUCHERTYPENAME><LEDGERENTRIES.LIST><LEDGERNAME>Customer</LEDGERNAME><ISDEEMEDPOSITIVE>Yes</ISDEEMEDPOSITIVE><AMOUNT>-105.00</AMOUNT></LEDGERENTRIES.LIST><ALLINVENTORYENTRIES.LIST><ACCOUNTINGALLOCATIONS.LIST><LEDGERNAME>SALE</LEDGERNAME><ISDEEMEDPOSITIVE>No</ISDEEMEDPOSITIVE><AMOUNT>100.00</AMOUNT></ACCOUNTINGALLOCATIONS.LIST></ALLINVENTORYENTRIES.LIST><LEDGERENTRIES.LIST><LEDGERNAME>Output Tax</LEDGERNAME><ISDEEMEDPOSITIVE>No</ISDEEMEDPOSITIVE><AMOUNT>5.00</AMOUNT></LEDGERENTRIES.LIST></VOUCHER></DATA></BODY></ENVELOPE>";
        var voucher = Assert.Single(new TallyXmlResponseParser().ParseVouchers(xml));
        Assert.Equal(3, voucher.Entries.Count);
        Assert.Equal(105.00m, voucher.Entries.Where(x => x.Amount.Direction == DebitCredit.Debit).Sum(x => x.Amount.Value));
        Assert.Equal(105.00m, voucher.Entries.Where(x => x.Amount.Direction == DebitCredit.Credit).Sum(x => x.Amount.Value));
    }

    [Fact] public void Preserves_negative_credit_side_rounding_amount()
    {
        const string xml = "<ENVELOPE><HEADER><STATUS>1</STATUS></HEADER><BODY><DATA><VOUCHER><MASTERID>round</MASTERID><DATE>20260901</DATE><VOUCHERNUMBER>R-1</VOUCHERNUMBER><VOUCHERTYPENAME>Sales</VOUCHERTYPENAME><LEDGERENTRIES.LIST><LEDGERNAME>Round Off</LEDGERNAME><ISDEEMEDPOSITIVE>No</ISDEEMEDPOSITIVE><AMOUNT>-0.02</AMOUNT></LEDGERENTRIES.LIST></VOUCHER></DATA></BODY></ENVELOPE>";
        var entry = new TallyXmlResponseParser().ParseVouchers(xml).Single().Entries.Single();
        Assert.Equal(DebitCredit.Credit, entry.Amount.Direction);
        Assert.Equal(0.02m, entry.Amount.Value);
        Assert.Equal(-0.02m, entry.SourceAmount);
    }
}
