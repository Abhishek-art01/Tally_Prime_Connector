using System.Security;
using TallyPrimeConnector.Contracts;

namespace TallyPrimeConnector.Tally;

/// <summary>Creates only read-only XML export requests verified against current TallyHelp documentation.</summary>
public static class TallyXmlRequestFactory
{
    public static string CreateLedgerListRequest(CompanyInfo? company = null)
    {
        var companyVariables = company is null ? string.Empty : $"<SVCURRENTCOMPANY>{SecurityElement.Escape(company.Name)}</SVCURRENTCOMPANY>";
        return $"<ENVELOPE><HEADER><VERSION>1</VERSION><TALLYREQUEST>EXPORT</TALLYREQUEST><TYPE>COLLECTION</TYPE><ID>List of Ledgers</ID></HEADER><BODY><DESC><STATICVARIABLES><SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>{companyVariables}</STATICVARIABLES></DESC></BODY></ENVELOPE>";
    }

    public static string CreateGroupListRequest(CompanyInfo? company = null)
    {
        var companyVariables = company is null ? string.Empty : $"<SVCURRENTCOMPANY>{SecurityElement.Escape(company.Name)}</SVCURRENTCOMPANY>";
        return $"<ENVELOPE><HEADER><VERSION>1</VERSION><TALLYREQUEST>EXPORT</TALLYREQUEST><TYPE>DATA</TYPE><ID>List of Accounts</ID></HEADER><BODY><DESC><STATICVARIABLES><SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT><AccountType>Groups</AccountType>{companyVariables}</STATICVARIABLES></DESC></BODY></ENVELOPE>";
    }

    public static string CreateDayBookRequest(CompanyInfo company, DateOnly from, DateOnly to)
    {
        static string Date(DateOnly value) => value.ToString("d-MMM-yyyy", System.Globalization.CultureInfo.InvariantCulture);
        return $"<ENVELOPE><HEADER><VERSION>1</VERSION><TALLYREQUEST>EXPORT</TALLYREQUEST><TYPE>DATA</TYPE><ID>DayBook</ID></HEADER><BODY><DESC><STATICVARIABLES><SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT><SVCURRENTCOMPANY>{SecurityElement.Escape(company.Name)}</SVCURRENTCOMPANY><SVFROMDATE TYPE=\"Date\">{Date(from)}</SVFROMDATE><SVTODATE TYPE=\"Date\">{Date(to)}</SVTODATE></STATICVARIABLES></DESC></BODY></ENVELOPE>";
    }

    /// <summary>
    /// Defines an ephemeral, read-only Voucher collection in the export request.
    /// The definition is evaluated for this request only; it is not installed in, or persisted by, TallyPrime.
    /// Tally's documented Voucher collection observes SVFROMDATE and SVTODATE.
    /// </summary>
    public static string CreateVoucherCollectionRequest(CompanyInfo company, DateOnly from, DateOnly to)
    {
        static string Date(DateOnly value) => value.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);

        return $"<ENVELOPE><HEADER><VERSION>1</VERSION><TALLYREQUEST>EXPORT</TALLYREQUEST><TYPE>COLLECTION</TYPE><ID>TPC Read Only Voucher Scope</ID></HEADER><BODY><DESC><STATICVARIABLES><SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT><SVCURRENTCOMPANY TYPE=\"String\">{SecurityElement.Escape(company.Name)}</SVCURRENTCOMPANY><SVFROMDATE TYPE=\"Date\">{Date(from)}</SVFROMDATE><SVTODATE TYPE=\"Date\">{Date(to)}</SVTODATE></STATICVARIABLES><TDL><TDLMESSAGE><COLLECTION NAME=\"TPC Read Only Voucher Scope\" ISMODIFY=\"No\" ISFIXED=\"No\" ISINITIALIZE=\"Yes\" ISOPTION=\"No\" ISINTERNAL=\"No\"><TYPE>Voucher</TYPE><FETCH>Date</FETCH><FETCH>GUID</FETCH><FETCH>MasterID</FETCH><FETCH>VoucherTypeName</FETCH><FETCH>VoucherNumber</FETCH><FETCH>Narration</FETCH><FETCH>PartyLedgerName</FETCH><FETCH>Reference</FETCH><FETCH>LedgerEntries.*</FETCH><FETCH>AllLedgerEntries.*</FETCH></COLLECTION></TDLMESSAGE></TDL></DESC></BODY></ENVELOPE>";
    }
}
