using Microsoft.EntityFrameworkCore;
using TallyPrimeConnector.Contracts;
namespace TallyPrimeConnector.Data;
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ConnectionProfileEntity> ConnectionProfiles => Set<ConnectionProfileEntity>();
    public DbSet<JobEntity> Jobs => Set<JobEntity>();
}
public sealed class ConnectionProfileEntity { public int Id { get; set; } public string Name { get; set; } = ""; public string Host { get; set; } = "localhost"; public int Port { get; set; } public TallyProtocol Protocol { get; set; } public TallyConnectionMethod Method { get; set; } }
public sealed class JobEntity { public Guid Id { get; set; } public string Type { get; set; } = ""; public JobStatus Status { get; set; } = JobStatus.Pending; public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; public int Progress { get; set; } }
