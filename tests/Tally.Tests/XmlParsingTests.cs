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
}
