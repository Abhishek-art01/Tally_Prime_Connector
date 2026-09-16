using ClosedXML.Excel;
using TallyPrimeConnector.Contracts;

namespace TallyPrimeConnector.Excel;

/// <summary>Writes a deterministic, ledger-per-worksheet workbook from normalized C# extraction data.</summary>
public sealed class LedgerWorkbookExporter : ILedgerWorkbookExporter
{
    private static readonly string[] Headers = ["Date", "Voucher Type", "Voucher Number", "Ledger", "Party Ledger", "Reference", "Narration", "Debit", "Credit", "Amount", "Source Amount", "GUID", "MasterID"];

    public async Task<string> ExportAsync(LedgerWiseExtractionResult extraction, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOutputPath(extraction.Request.OutputPath);

        using var workbook = new XLWorkbook();
        var worksheetNames = CreateWorksheetNames(extraction.Ledgers.Select(x => x.Ledger.Name));
        for (var ledgerIndex = 0; ledgerIndex < extraction.Ledgers.Count; ledgerIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteLedgerWorksheet(workbook.Worksheets.Add(worksheetNames[ledgerIndex]), extraction.Ledgers[ledgerIndex], cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        workbook.SaveAs(extraction.Request.OutputPath);
        await Task.CompletedTask;
        return extraction.Request.OutputPath;
    }

    // Retained for the existing general exporter contract. Ledger-wise exports should use the overload above.
    public async Task<string> ExportAsync(IEnumerable<VoucherInfo> vouchers, string outputPath, CancellationToken cancellationToken)
    {
        var materialized = vouchers.ToList();
        var ledgers = materialized.SelectMany(x => x.Entries).Select(x => x.LedgerName).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select((name, index) => new LedgerInfo($"legacy-{index}", name)).ToList();
        var exportDate = materialized.Count == 0 ? DateOnly.FromDateTime(DateTime.Today) : materialized.Min(x => x.Date);
        var request = LedgerWiseExtractionRequest.Create(new CompanyContext(new CompanyInfo("legacy", "Legacy export"), new ConnectionProfile("legacy", "localhost", 0, TallyProtocol.HttpXml, TallyConnectionMethod.XmlHttp)), DateRange.Create(exportDate, materialized.Count == 0 ? exportDate : materialized.Max(x => x.Date)), null, ledgers, outputPath);
        var results = ledgers.Select(ledger => BuildLegacyLedgerResult(ledger, materialized)).ToList();
        var result = new LedgerWiseExtractionResult(request, [], results, materialized.Count, results.Sum(x => x.TransactionCount), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new ExtractionReconciliation(0m, 0m, ReconciliationStatus.NotApplicable, []));
        return await ExportAsync(result, cancellationToken);
    }

    public static string SafeName(string value)
    {
        var clean = string.Concat(value.Select(c => "[]:*?/\\".Contains(c) ? '_' : c)).Trim();
        return string.IsNullOrEmpty(clean) ? "Ledger" : clean[..Math.Min(31, clean.Length)];
    }

    public static IReadOnlyList<string> CreateWorksheetNames(IEnumerable<string> ledgerNames)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>();
        foreach (var ledgerName in ledgerNames)
        {
            var baseName = SafeName(ledgerName);
            var candidate = baseName;
            var suffix = 2;
            while (!used.Add(candidate))
            {
                var postfix = $" ({suffix++})";
                candidate = baseName[..Math.Min(baseName.Length, 31 - postfix.Length)] + postfix;
            }
            names.Add(candidate);
        }
        return names;
    }

    private static void WriteLedgerWorksheet(IXLWorksheet worksheet, LedgerResult ledger, CancellationToken cancellationToken)
    {
        for (var column = 0; column < Headers.Length; column++) worksheet.Cell(1, column + 1).Value = Headers[column];
        var header = worksheet.Range(1, 1, 1, Headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("1F4E78");
        header.Style.Font.FontColor = XLColor.White;
        worksheet.SheetView.FreezeRows(1);

        var row = 2;
        foreach (var transaction in ledger.Transactions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var voucher = transaction.Voucher;
            var entry = transaction.Entry;
            worksheet.Cell(row, 1).Value = voucher.Date.ToDateTime(TimeOnly.MinValue);
            worksheet.Cell(row, 2).Value = voucher.VoucherType;
            worksheet.Cell(row, 3).Value = voucher.VoucherNumber;
            worksheet.Cell(row, 4).Value = ledger.Ledger.Name;
            worksheet.Cell(row, 5).Value = voucher.PartyLedgerName ?? voucher.Party?.Name;
            worksheet.Cell(row, 6).Value = entry.Reference ?? voucher.ReferenceNumber;
            worksheet.Cell(row, 7).Value = voucher.Narration;
            worksheet.Cell(row, 8).Value = entry.Amount.Direction == DebitCredit.Debit ? entry.Amount.Value : 0m;
            worksheet.Cell(row, 9).Value = entry.Amount.Direction == DebitCredit.Credit ? entry.Amount.Value : 0m;
            worksheet.Cell(row, 10).Value = entry.Amount.Value;
            worksheet.Cell(row, 11).Value = entry.SourceAmount;
            worksheet.Cell(row, 12).Value = voucher.Guid;
            worksheet.Cell(row, 13).Value = voucher.MasterId ?? voucher.SourceId;
            row++;
        }

        if (ledger.Transactions.Count == 0)
        {
            worksheet.Cell(row, 1).Value = "No matching transactions found for this selected ledger.";
            worksheet.Range(row, 1, row, Headers.Length).Merge();
            worksheet.Cell(row, 1).Style.Font.Italic = true;
        }

        worksheet.Range(1, 1, Math.Max(1, row - 1), Headers.Length).SetAutoFilter();
        worksheet.Column(1).Style.NumberFormat.Format = "yyyy-mm-dd";
        worksheet.Range(2, 8, Math.Max(2, row - 1), 11).Style.NumberFormat.Format = "#,##0.00;[Red]-#,##0.00";
        worksheet.Column(1).Width = 12;
        worksheet.Column(2).Width = 18;
        worksheet.Column(3).Width = 19;
        worksheet.Column(4).Width = 28;
        worksheet.Column(5).Width = 28;
        worksheet.Column(6).Width = 20;
        worksheet.Column(7).Width = 42;
        worksheet.Columns(8, 11).Width = 14;
        worksheet.Column(12).Width = 38;
        worksheet.Column(13).Width = 16;
    }

    private static LedgerResult BuildLegacyLedgerResult(LedgerInfo ledger, IReadOnlyList<VoucherInfo> vouchers)
    {
        var transactions = vouchers.SelectMany(voucher => voucher.Entries.Select((entry, index) => new { voucher, entry, index }))
            .Where(x => string.Equals(x.entry.LedgerName.Trim(), ledger.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(x => new LedgerTransaction(x.voucher, x.entry, x.index))
            .ToList();
        return new LedgerResult(ledger, transactions, transactions.Where(x => x.Entry.Amount.Direction == DebitCredit.Debit).Sum(x => x.Entry.Amount.Value), transactions.Where(x => x.Entry.Amount.Direction == DebitCredit.Credit).Sum(x => x.Entry.Amount.Value));
    }

    private static void ValidateOutputPath(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath)) throw new ExportException("An Excel output path is required.");
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) throw new ExportException("The Excel output directory does not exist.");
        if (!string.Equals(Path.GetExtension(outputPath), ".xlsx", StringComparison.OrdinalIgnoreCase)) throw new ExportException("The Excel output path must use the .xlsx extension.");
    }
}
