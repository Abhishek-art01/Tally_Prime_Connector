using TallyPrimeConnector.Contracts;
using TallyPrimeConnector.Processing.Core;
using TallyPrimeConnector.Processing.Specialist;

namespace TallyPrimeConnector.Processing;

public enum ProcessingEngineKind { CoreCSharp, PythonSpecialist }
public sealed record ProcessingRequest(string JobType, IReadOnlyList<VoucherInfo> Vouchers, ExtractionMetadata? Metadata = null);
public sealed record SpecialistProcessingRequest(string JobType, IReadOnlyList<VoucherInfo> NormalizedVouchers, ExtractionMetadata? Metadata = null, IReadOnlyDictionary<string, string>? Options = null);
public sealed record SpecialistProcessingResult(bool IsAvailable, IReadOnlyDictionary<string, decimal> Metrics, IReadOnlyList<string> Findings, string? Error = null)
{
    public static SpecialistProcessingResult Unavailable(string message) => new(false, new Dictionary<string, decimal>(), [], message);
}

/// <summary>The C# application selects and orchestrates the engine for every processing job.</summary>
public interface IProcessingEngineSelector
{
    ProcessingEngineKind Select(string jobType);
    Task<ProcessingResult> ProcessCoreAsync(ProcessingRequest request, CancellationToken cancellationToken);
    Task<SpecialistProcessingResult> ProcessSpecialistAsync(SpecialistProcessingRequest request, CancellationToken cancellationToken);
}

public sealed class ProcessingEngineSelector(ICoreProcessingEngine core, ISpecialistProcessingEngine specialist) : IProcessingEngineSelector
{
    public ProcessingEngineKind Select(string jobType) => jobType.StartsWith("specialist.", StringComparison.OrdinalIgnoreCase) ? ProcessingEngineKind.PythonSpecialist : ProcessingEngineKind.CoreCSharp;
    public Task<ProcessingResult> ProcessCoreAsync(ProcessingRequest request, CancellationToken cancellationToken) => core.ProcessAsync(request, cancellationToken);
    public Task<SpecialistProcessingResult> ProcessSpecialistAsync(SpecialistProcessingRequest request, CancellationToken cancellationToken) => specialist.ProcessAsync(request, cancellationToken);
}
