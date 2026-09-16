using TallyPrimeConnector.Contracts;
using TallyPrimeConnector.Processing;

namespace TallyPrimeConnector.Processing.Specialist;

/// <summary>
/// Boundary to the bundled Python specialist engine. Python receives normalized DTOs only;
/// it never connects to TallyPrime, SQLite, or the desktop UI.
/// </summary>
public interface ISpecialistProcessingEngine
{
    Task<SpecialistProcessingResult> ProcessAsync(SpecialistProcessingRequest request, CancellationToken cancellationToken);
}

/// <summary>Implemented later by a local bundled-runtime host, never by a user's global Python PATH.</summary>
public interface IPythonSpecialistRuntime
{
    Task<PythonRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken);
    Task<SpecialistProcessingResult> ExecuteAsync(SpecialistProcessingRequest request, CancellationToken cancellationToken);
}

public sealed record PythonRuntimeStatus(bool IsAvailable, string Message, string? RuntimeVersion = null);

public sealed class PythonSpecialistProcessingEngine(IPythonSpecialistRuntime runtime) : ISpecialistProcessingEngine
{
    public async Task<SpecialistProcessingResult> ProcessAsync(SpecialistProcessingRequest request, CancellationToken cancellationToken)
    {
        var status = await runtime.GetStatusAsync(cancellationToken);
        return status.IsAvailable
            ? await runtime.ExecuteAsync(request, cancellationToken)
            : SpecialistProcessingResult.Unavailable(status.Message);
    }
}

/// <summary>Phase 1.1 safe host: records that the required bundled runtime has not been packaged yet.</summary>
public sealed class UnavailablePythonSpecialistRuntime : IPythonSpecialistRuntime
{
    private const string Message = "Python specialist processing is unavailable because the bundled runtime is not installed.";
    public Task<PythonRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken) => Task.FromResult(new PythonRuntimeStatus(false, Message));
    public Task<SpecialistProcessingResult> ExecuteAsync(SpecialistProcessingRequest request, CancellationToken cancellationToken) => Task.FromResult(SpecialistProcessingResult.Unavailable(Message));
}
