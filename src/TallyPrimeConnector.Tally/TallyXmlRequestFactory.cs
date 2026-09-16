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
}
