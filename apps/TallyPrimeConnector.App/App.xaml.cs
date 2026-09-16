using System.Windows;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using TallyPrimeConnector.Contracts;
using TallyPrimeConnector.Core;
using TallyPrimeConnector.Data;
using TallyPrimeConnector.Excel;
using TallyPrimeConnector.Processing;
using TallyPrimeConnector.Processing.Core;
using TallyPrimeConnector.Processing.Specialist;
using TallyPrimeConnector.Tally;
namespace TallyPrimeConnector.App;
public partial class App : Application
{
 private IHost? _host;
 protected override async void OnStartup(StartupEventArgs e) { base.OnStartup(e); _host = Host.CreateDefaultBuilder().UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.File(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tally Prime Connector", "logs", "app-.log"), rollingInterval: RollingInterval.Day)).ConfigureServices((context, services) => { var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tally Prime Connector"); Directory.CreateDirectory(dataDirectory); var liveProfile = new ConnectionProfile("Configured TallyPrime", context.Configuration["Connection:Host"] ?? "localhost", int.TryParse(context.Configuration["Connection:Port"], out var configuredPort) ? configuredPort : 9000, TallyProtocol.HttpXml, TallyConnectionMethod.XmlHttp); services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={Path.Combine(dataDirectory, "connector.db")}")); services.AddSingleton<ITallyXmlClient, TallyXmlHttpClient>(); services.AddSingleton<TallyXmlResponseParser>(); services.AddSingleton<ITallyConnection, TallyXmlHttpConnection>(); services.AddSingleton<ITallyCompanyProvider>(serviceProvider => new TallyXmlCompanyProvider(serviceProvider.GetRequiredService<ITallyXmlClient>(), serviceProvider.GetRequiredService<TallyXmlResponseParser>())); services.AddSingleton<ITallyCollectionProvider>(serviceProvider => new TallyXmlCollectionProvider(serviceProvider.GetRequiredService<ITallyXmlClient>(), serviceProvider.GetRequiredService<TallyXmlResponseParser>(), liveProfile)); services.AddSingleton<ITallyVoucherProvider>(serviceProvider => new TallyXmlVoucherProvider(serviceProvider.GetRequiredService<ITallyXmlClient>(), serviceProvider.GetRequiredService<TallyXmlResponseParser>(), liveProfile)); services.AddSingleton<IConnectionService, ConnectionService>(); services.AddSingleton<ICompanyService, CompanyService>(); services.AddSingleton<IGroupService, GroupService>(); services.AddSingleton<ILedgerService, LedgerService>(); services.AddSingleton<IVoucherService, VoucherService>(); services.AddSingleton<IExtractionService, ExtractionService>(); services.AddSingleton<ILedgerWiseExtractionService, LedgerWiseExtractionService>(); services.AddSingleton<ILedgerWorkbookExporter, LedgerWorkbookExporter>(); services.AddSingleton<ILedgerWiseExportService, LedgerWiseExportService>(); services.AddSingleton<IValidationProcessor, BasicValidationProcessor>(); services.AddSingleton<IProcessingPipeline, StandardProcessingPipeline>(); services.AddSingleton<ICoreProcessingEngine, CoreProcessingEngine>(); services.AddSingleton<IPythonSpecialistRuntime, UnavailablePythonSpecialistRuntime>(); services.AddSingleton<ISpecialistProcessingEngine, PythonSpecialistProcessingEngine>(); services.AddSingleton<IProcessingEngineSelector, ProcessingEngineSelector>(); services.AddSingleton<MainViewModel>(); services.AddSingleton<MainWindow>(); }).Build(); await _host.StartAsync(); Log.Information("Application started"); _host.Services.GetRequiredService<MainWindow>().Show(); }
 protected override async void OnExit(ExitEventArgs e) { Log.Information("Application shutdown"); if (_host is not null) await _host.StopAsync(); Log.CloseAndFlush(); base.OnExit(e); }
}
