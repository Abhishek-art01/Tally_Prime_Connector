using TallyPrimeConnector.Excel;
namespace Excel.Tests;
public sealed class ExcelTests { [Fact] public void Sheet_names_are_sanitized_and_limited() { var value = LedgerWorkbookExporter.SafeName("A/B:C*D?E[F]G\\H"); Assert.DoesNotContain('/', value); Assert.True(value.Length <= 31); } }
