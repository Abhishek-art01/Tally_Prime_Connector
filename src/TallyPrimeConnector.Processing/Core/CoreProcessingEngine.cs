using TallyPrimeConnector.Contracts;
using TallyPrimeConnector.Processing;

namespace TallyPrimeConnector.Processing.Core;

/// <summary>Primary in-process engine for normal accounting and application workflows.</summary>
public interface ICoreProcessingEngine
{
    Task<ProcessingResult> ProcessAsync(ProcessingRequest request, CancellationToken cancellationToken);
}

public sealed class CoreProcessingEngine(IProcessingPipeline pipeline) : ICoreProcessingEngine
{
    public Task<ProcessingResult> ProcessAsync(ProcessingRequest request, CancellationToken cancellationToken) =>
        pipeline.ProcessAsync(request.Vouchers, cancellationToken);
}

/// <summary>Default C# pipeline. It deliberately has no Python dependency.</summary>
public sealed class StandardProcessingPipeline(IValidationProcessor validationProcessor) : IProcessingPipeline
{
    public Task<ProcessingResult> ProcessAsync(IReadOnlyList<VoucherInfo> vouchers, CancellationToken cancellationToken) =>
        validationProcessor.ProcessAsync(vouchers, cancellationToken);
}
