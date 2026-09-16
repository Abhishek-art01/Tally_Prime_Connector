using Microsoft.Extensions.Logging;
using TallyPrimeConnector.Contracts;

namespace TallyPrimeConnector.Core;

/// <summary>Coordinates Core extraction with the contract-level Excel writer without exposing Tally protocol details.</summary>
public sealed class LedgerWiseExportService(ILedgerWiseExtractionService extraction, ILedgerWorkbookExporter workbookExporter, ILogger<LedgerWiseExportService> logger) : ILedgerWiseExportService
{
    public async Task<LedgerWiseExportResult> ExtractAndExportAsync(LedgerWiseExtractionRequest request, IProgress<ExtractionProgress>? progress, CancellationToken cancellationToken)
    {
        var result = await extraction.ExtractAsync(request, progress, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Writing ledger workbook for {Company} to {OutputPath}.", request.CompanyContext.Company.Name, request.OutputPath);
        progress?.Report(new ExtractionProgress("Writing ledger workbook", result.TotalVoucherCount, result.TotalMatchedTransactionCount, 96, false, result.Batches.Count, result.Batches.Count, request.DateRange, result.TotalMatchedTransactionCount));
        var outputPath = await workbookExporter.ExportAsync(result, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Ledger workbook completed: {OutputPath}.", outputPath);
        progress?.Report(new ExtractionProgress("Ledger workbook completed", result.TotalVoucherCount, result.TotalMatchedTransactionCount, 100, true, result.Batches.Count, result.Batches.Count, request.DateRange, result.TotalMatchedTransactionCount));
        return new LedgerWiseExportResult(result, outputPath);
    }
}
