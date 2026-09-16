using TallyPrimeConnector.Contracts;
namespace TallyPrimeConnector.Infrastructure;
public sealed class NoOpUpdateService : IUpdateService { public Task<bool> IsUpdateAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(false); }
