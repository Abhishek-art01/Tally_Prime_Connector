using ClosedXML.Excel;
using TallyPrimeConnector.Contracts;
using TallyPrimeConnector.Excel;

namespace Excel.Tests;

public sealed class ExcelTests
{
    [Fact]
    public void Worksheet_names_are_sanitized_limited_and_deduplicated()
    {
        var names = LedgerWorkbookExporter.CreateWorksheetNames(["A/B:C*D?E[F]G\\H", "A_B_C_D_E_F_G_H", new string('X', 40)]);

        Assert.Equal(3, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(names, x => Assert.True(x.Length <= 31));
        Assert.DoesNotContain('/', names[0]);
        Assert.EndsWith(" (2)", names[1]);
    }

    [Fact]
    public async Task Ledger_workbook_has_selected_and_empty_ledger_sheets_with_exact_decimal_columns()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"tally-ledger-{Guid.NewGuid():N}.xlsx");
        try
        {
            var voucher = new VoucherInfo("source", "AR/1", new DateOnly(2026, 9, 1), "Sales", "Narration", [new VoucherEntryInfo("entry", "G Pay", new TransactionAmount(12.34m, DebitCredit.Debit), "REF-1", -12.34m)], PartyLedgerName: "G Pay", ReferenceNumber: "REF-1", Guid: "voucher-guid", MasterId: "voucher-master");
            var request = LedgerWiseExtractionRequest.Create(new CompanyContext(new CompanyInfo("company", "Company"), new ConnectionProfile("test", "localhost", 9000, TallyProtocol.HttpXml, TallyConnectionMethod.XmlHttp)), DateRange.Create(voucher.Date, voucher.Date), null, [new("gpay", "G Pay"), new("empty", "Empty/Ledger")], outputPath);
            var gPay = new LedgerResult(request.SelectedLedgers[0], [new LedgerTransaction(voucher, voucher.Entries[0], 0)], 12.34m, 0m);
            var empty = new LedgerResult(request.SelectedLedgers[1], [], 0m, 0m);
            var result = new LedgerWiseExtractionResult(request, [new ExtractionBatch(request.DateRange, 1, 1, 1)], [gPay, empty], 1, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new ExtractionReconciliation(12.34m, 12.34m, ReconciliationStatus.Balanced, []));

            await new LedgerWorkbookExporter().ExportAsync(result, CancellationToken.None);

            Assert.True(File.Exists(outputPath));
            using var workbook = new XLWorkbook(outputPath);
            Assert.Equal(2, workbook.Worksheets.Count);
            var sheet = workbook.Worksheet("G Pay");
            Assert.Equal("Date", sheet.Cell(1, 1).GetString());
            Assert.Equal("Source Amount", sheet.Cell(1, 11).GetString());
            Assert.Equal(12.34m, sheet.Cell(2, 8).GetValue<decimal>());
            Assert.Equal(-12.34m, sheet.Cell(2, 11).GetValue<decimal>());
            Assert.Equal("voucher-guid", sheet.Cell(2, 12).GetString());
            var emptySheet = workbook.Worksheet("Empty_Ledger");
            Assert.Contains("No matching transactions", emptySheet.Cell(2, 1).GetString());
        }
        finally { if (File.Exists(outputPath)) File.Delete(outputPath); }
    }
}
