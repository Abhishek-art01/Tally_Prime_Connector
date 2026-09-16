using TallyPrimeConnector.Processing.Specialist;
namespace Processing.Tests;
public sealed class SpecialistProcessingTests
{
    [Fact]
    public async Task Unbundled_runtime_reports_unavailable_without_affecting_core_processing()
    {
        var engine = new PythonSpecialistProcessingEngine(new UnavailablePythonSpecialistRuntime());
        var result = await engine.ProcessAsync(new("specialist.anomaly", []), CancellationToken.None);
        Assert.False(result.IsAvailable);
        Assert.Contains("bundled runtime", result.Error ?? string.Empty);
    }
}
