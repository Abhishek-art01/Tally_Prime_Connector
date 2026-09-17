using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using TallyPrimeConnector.Contracts;
using TallyPrimeConnector.Core;

namespace TallyPrimeConnector.App;

public sealed class LedgerSelectionItem(LedgerInfo ledger) : INotifyPropertyChanged
{
    private bool _isSelected;
    public LedgerInfo Ledger { get; } = ledger;
    public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new(nameof(IsSelected))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class MainViewModel(IConnectionService connectionService, ICompanyService companyService, IGroupService groupService, ILedgerService ledgerService, ILedgerWiseExportService exportService) : INotifyPropertyChanged
{
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private CancellationTokenSource? _extractionCancellation;
    private string _selectedPage = "Dashboard", _host = "localhost", _port = "9000", _connectionResult = "No connection test has been run.", _ledgerSearch = "", _placeholderLedgerText = "Search ledgers...", _batchDays = LedgerWiseExtractionRequest.DefaultBatchDays.ToString(), _exportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Tally Ledger Export.xlsx"), _lastExportPath = "", _extractionStatus = "Select a company, dates, one or more ledgers, and an output file.";
    private DateTime? _fromDate = DateTime.Today, _toDate = DateTime.Today;
    private bool _isSidebarCollapsed;
    private CompanyInfo? _selectedCompany;
    private GroupInfo? _selectedGroup;

    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<string> NavigationItems { get; } = ["Dashboard"];
    public ObservableCollection<CompanyInfo> Companies { get; } = [];
    public ObservableCollection<GroupInfo> Groups { get; } = [];
    public ObservableCollection<LedgerInfo> Ledgers { get; } = [];
    public ObservableCollection<LedgerSelectionItem> LedgerSelections { get; } = [];
    public ObservableCollection<VoucherInfo> Vouchers { get; } = [];

    public string SelectedPage { get => _selectedPage; set { _selectedPage = value; OnChanged(); OnChanged(nameof(PageDescription)); } }
    public string PageDescription => SelectedPage == "Dashboard" ? "Read-only Tally extraction and ledger-wise Excel export." : "Configure and manage " + SelectedPage.ToLowerInvariant() + ".";
    public string Host { get => _host; set { _host = value; OnChanged(); } }
    public string Port { get => _port; set { _port = value; OnChanged(); } }
    public string ConnectionResult { get => _connectionResult; set { _connectionResult = value; OnChanged(); } }
    public string LedgerSearch { get => _ledgerSearch; set { _ledgerSearch = value; OnChanged(); OnChanged(nameof(FilteredLedgerSelections)); } }
    public string PlaceholderLedgerText { get => _placeholderLedgerText; set { _placeholderLedgerText = value; OnChanged(); } }
    public IEnumerable<LedgerSelectionItem> FilteredLedgerSelections => LedgerSelections.Where(x => string.IsNullOrWhiteSpace(LedgerSearch) || x.Ledger.Name.Contains(LedgerSearch, StringComparison.OrdinalIgnoreCase));
    public DateTime? FromDate { get => _fromDate; set { _fromDate = value; OnChanged(); } }
    public DateTime? ToDate { get => _toDate; set { _toDate = value; OnChanged(); } }
    public string BatchDays { get => _batchDays; set { _batchDays = value; OnChanged(); } }
    public string ExportPath { get => _exportPath; set { _exportPath = value; OnChanged(); } }
    public string LastExportPath { get => _lastExportPath; private set { _lastExportPath = value; OnChanged(); } }
    public string ExtractionStatus { get => _extractionStatus; set { _extractionStatus = value; OnChanged(); } }
    public CompanyInfo? SelectedCompany { get => _selectedCompany; set { _selectedCompany = value; OnChanged(); } }
    public GroupInfo? SelectedGroup { get => _selectedGroup; set { _selectedGroup = value; OnChanged(); } }
    public GridLength SidebarWidth => new(_isSidebarCollapsed ? 0 : 208);

    public ICommand TestConnectionCommand => new AsyncCommand(TestConnectionAsync);
    public ICommand LoadCompaniesCommand => new AsyncCommand(LoadCompaniesAsync);
    public ICommand LoadGroupsCommand => new AsyncCommand(LoadGroupsAsync);
    public ICommand LoadLedgersCommand => new AsyncCommand(LoadLedgersAsync);
    public ICommand SelectAllLedgersCommand => new RelayCommand(SelectAllLedgers);
    public ICommand ChooseExportPathCommand => new RelayCommand(ChooseExportPath);
    public ICommand RevealExportCommand => new RelayCommand(RevealExport);
    public ICommand ExtractCommand => new AsyncCommand(ExtractAndExportAsync);
    public ICommand CancelExtractionCommand => new RelayCommand(() => _extractionCancellation?.Cancel());
    public ICommand ToggleSidebarCommand => new RelayCommand(() =>
    {
        _isSidebarCollapsed = !_isSidebarCollapsed;
        OnChanged(nameof(SidebarWidth));
    });

    private ConnectionProfile Profile() => int.TryParse(Port, out var port) && port is > 0 and <= 65535
        ? new("Default", Host, port, TallyProtocol.HttpXml, TallyConnectionMethod.XmlHttp)
        : throw new ConfigurationException("Please enter a valid port between 1 and 65535.");

    private async Task TestConnectionAsync()
    {
        try
        {
            var result = await connectionService.TestAsync(Profile(), _lifetimeCancellation.Token);
            ConnectionResult = string.Join(Environment.NewLine, result.Diagnostics.Select(x => $"{(x.Passed ? "✓" : "•")} {x.Check}: {x.Message}"));
        }
        catch (ConfigurationException exception) { ConnectionResult = exception.Message; }
        catch { ConnectionResult = "TallyPrime is not responding on the configured HTTP/XML port."; }
    }

    private async Task LoadCompaniesAsync()
    {
        try
        {
            Companies.Clear();
            foreach (var company in await companyService.GetCompaniesAsync(Profile(), _lifetimeCancellation.Token)) Companies.Add(company);
            SelectedCompany = Companies.FirstOrDefault();
            ExtractionStatus = Companies.Count == 0 ? "No companies were returned. Confirm that a company is loaded in TallyPrime." : $"{Companies.Count} company loaded. Select it, then load groups and ledgers.";
        }
        catch (ConfigurationException exception) { ExtractionStatus = exception.Message; }
        catch (Exception exception) { ExtractionStatus = "Unable to load companies from TallyPrime: " + exception.Message; }
    }

    private async Task LoadGroupsAsync()
    {
        if (!await EnsureCompanyAsync()) return;

        try
        {
            Groups.Clear();
            foreach (var group in await groupService.GetGroupsAsync(SelectedCompany!, _lifetimeCancellation.Token)) Groups.Add(group);
            SelectedGroup = Groups.FirstOrDefault();
            ExtractionStatus = Groups.Count == 0
                ? "No groups were returned, so ledgers cannot be scoped to a selected group."
                : $"{Groups.Count} groups loaded. Choose a group, then load only its ledgers.";
        }
        catch (Exception exception) { ExtractionStatus = "Unable to load groups from TallyPrime: " + exception.Message; }
    }

    private async Task LoadLedgersAsync()
    {
        if (!await EnsureCompanyAsync()) return;
        if (SelectedGroup is null)
        {
            if (Groups.Count == 0)
            {
                await LoadGroupsAsync();
            }

            if (SelectedGroup is null)
            {
                ExtractionStatus = "Select a group before loading ledgers. The ledger list is scoped to the selected group only.";
                return;
            }
        }

        var selectedGroup = SelectedGroup!;

        try
        {
            Ledgers.Clear();
            LedgerSelections.Clear();
            foreach (var ledger in await ledgerService.GetLedgersAsync(SelectedCompany!, selectedGroup.Name, _lifetimeCancellation.Token))
            {
                Ledgers.Add(ledger);
                LedgerSelections.Add(new LedgerSelectionItem(ledger));
            }
            OnChanged(nameof(FilteredLedgerSelections));
            ExtractionStatus = LedgerSelections.Count == 0
                ? $"No ledgers belong to the selected group '{selectedGroup.Name}'."
                : $"{LedgerSelections.Count} ledgers loaded from '{selectedGroup.Name}'. Tick one or more exact ledger names for export.";

            PlaceholderLedgerText = LedgerSelections.Count > 0 ? $"{LedgerSelections.Count} ledgers loaded (e.g. {LedgerSelections[0].Ledger.Name})" : "Search ledgers...";
        }
        catch (Exception exception) { ExtractionStatus = "Unable to load ledgers from TallyPrime: " + exception.Message; }
    }

    private async Task<bool> EnsureCompanyAsync()
    {
        if (SelectedCompany is not null) return true;

        await LoadCompaniesAsync();
        if (SelectedCompany is not null) return true;

        ExtractionStatus = "No company is selected. Load a company in TallyPrime, then click Load Companies or Load Ledgers.";
        return false;
    }

    private void SelectAllLedgers()
    {
        foreach (var ledger in LedgerSelections)
        {
            ledger.IsSelected = true;
        }

        ExtractionStatus = LedgerSelections.Count == 0
            ? "No ledgers are loaded to select."
            : $"All {LedgerSelections.Count} loaded ledgers are selected.";
    }

    private void ChooseExportPath()
    {
        var dialog = new SaveFileDialog { Filter = "Excel Workbook (*.xlsx)|*.xlsx", DefaultExt = ".xlsx", AddExtension = true, FileName = Path.GetFileName(ExportPath) };
        if (dialog.ShowDialog() == true) ExportPath = dialog.FileName;
    }

    private void RevealExport()
    {
        if (string.IsNullOrWhiteSpace(LastExportPath) || !File.Exists(LastExportPath)) { ExtractionStatus = "No generated workbook is available to reveal."; return; }
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{LastExportPath}\"") { UseShellExecute = true }); }
        catch { ExtractionStatus = "The workbook was created, but its folder could not be opened automatically."; }
    }

    private async Task ExtractAndExportAsync()
    {
        if (SelectedCompany is null) { ExtractionStatus = "The selected company is not available."; return; }
        if (FromDate is not { } fromDate || ToDate is not { } toDate) { ExtractionStatus = "Select both dates using the calendar."; return; }
        var from = DateOnly.FromDateTime(fromDate);
        var to = DateOnly.FromDateTime(toDate);
        if (from > to) { ExtractionStatus = "From Date must be on or before To Date."; return; }
        if (!int.TryParse(BatchDays, out var batchDays) || batchDays < 1) { ExtractionStatus = "Batch size must be at least one day."; return; }
        var selectedLedgers = LedgerSelections.Where(x => x.IsSelected).Select(x => x.Ledger).ToList();
        if (selectedLedgers.Count == 0) { ExtractionStatus = "No selected ledgers were provided."; return; }

        try
        {
            var request = LedgerWiseExtractionRequest.Create(new CompanyContext(SelectedCompany, Profile()), DateRange.Create(from, to), SelectedGroup?.Name, selectedLedgers, ExportPath, batchDays);
            _extractionCancellation?.Dispose();
            _extractionCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
            var progress = new Progress<ExtractionProgress>(x => ExtractionStatus = $"{x.Stage} — batch {x.CurrentBatch}/{x.TotalBatches}; range {x.CurrentDateRange?.From:yyyy-MM-dd} to {x.CurrentDateRange?.To:yyyy-MM-dd}; vouchers {x.RecordsFound}; matched {x.MatchedTransactions}; {x.Percent}%");
            var result = await exportService.ExtractAndExportAsync(request, progress, _extractionCancellation.Token);
            Vouchers.Clear();
            foreach (var voucher in result.Extraction.Ledgers.SelectMany(x => x.Transactions).Select(x => x.Voucher).DistinctBy(x => x.Guid ?? x.MasterId ?? x.SourceId)) Vouchers.Add(voucher);
            LastExportPath = result.OutputPath;
            ExtractionStatus = $"Completed read-only export. {result.Extraction.TotalVoucherCount} vouchers, {result.Extraction.TotalMatchedTransactionCount} ledger transactions. Workbook: {result.OutputPath}";
        }
        catch (OperationCanceledException) { ExtractionStatus = "Extraction cancelled. No completion result was generated."; }
        catch (TallyProtocolException exception) { ExtractionStatus = "Tally returned data outside the requested date scope. Extraction stopped: " + exception.Message; }
        catch (ExtractionException exception) { ExtractionStatus = exception.Message; }
        catch (ExportException exception) { ExtractionStatus = exception.Message; }
        catch (Exception) { ExtractionStatus = "Unable to extract and export. See application logs for technical details."; }
    }

    private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public sealed class AsyncCommand(Func<Task> execute) : ICommand
{
    private bool _busy;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_busy;
    public async void Execute(object? parameter) { _busy = true; CanExecuteChanged?.Invoke(this, EventArgs.Empty); try { await execute(); } finally { _busy = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); } }
}

public sealed class RelayCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}
