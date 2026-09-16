using ClosedXML.Excel;
using TallyPrimeConnector.Contracts;
namespace TallyPrimeConnector.Excel;
public sealed class LedgerWorkbookExporter : ILedgerWorkbookExporter
{
 public Task<string> ExportAsync(IEnumerable<VoucherInfo> vouchers, string outputPath, CancellationToken cancellationToken) { using var workbook = new XLWorkbook(); foreach (var group in vouchers.SelectMany(v => v.Entries.Select(e => (v, e))).GroupBy(x => x.e.LedgerName)) { cancellationToken.ThrowIfCancellationRequested(); var sheet = workbook.Worksheets.Add(SafeName(group.Key)); sheet.Cell(1, 1).Value = "Date"; sheet.Cell(1, 2).Value = "Voucher"; sheet.Cell(1, 3).Value = "Amount"; var row = 2; foreach (var (voucher, entry) in group) { sheet.Cell(row, 1).Value = voucher.Date.ToDateTime(TimeOnly.MinValue); sheet.Cell(row, 2).Value = voucher.VoucherNumber; sheet.Cell(row++, 3).Value = entry.Amount.Value; } sheet.Columns().AdjustToContents(); } workbook.SaveAs(outputPath); return Task.FromResult(outputPath); }
 public static string SafeName(string value) { var clean = string.Concat(value.Select(c => "[]:*?/\\".Contains(c) ? '_' : c)).Trim(); return string.IsNullOrEmpty(clean) ? "Ledger" : clean[..Math.Min(31, clean.Length)]; }
}
