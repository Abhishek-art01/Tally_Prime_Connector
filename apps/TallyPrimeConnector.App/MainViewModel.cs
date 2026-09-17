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

public sealed class CompanySelectionItem(CompanyInfo company) : INotifyPropertyChanged
{
    private bool _isSelected;
    public CompanyInfo Company { get; } = company;
    public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new(nameof(IsSelected))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class GroupSelectionItem(GroupInfo group) : INotifyPropertyChanged
{
    private bool _isSelected;
    public GroupInfo Group { get; } = group;
    public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new(nameof(IsSelected))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class MainViewModel(IConnectionService connectionService, ICompanyService companyService, IGroupService groupService, ILedgerService ledgerService, ILedgerWiseExportService exportService) : INotifyPropertyChanged
{
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private CancellationTokenSource? _extractionCancellation;
    private string _selectedPage = "Dashboard", _host = "localhost", _port = "9000", _connectionResult = "No connection test has been run.", _ledgerSearch = "", _placeholderLedgerText = "Search ledgers...", _batchDays = LedgerWiseExtractionRequest.DefaultBatchDays.ToString(), _exportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Tally Ledger Export.xlsx"), _lastExportPath = "", _extractionStatus = "Select a company, dates, one or more ledgers, and an output file.";
    private string _companySearch = "", _placeholderCompanyText = "Search companies...", _groupSearch = "", _placeholderGroupText = "Search groups...";
    private DateTime? _fromDate = DateTime.Today, _toDate = DateTime.Today;
    private bool _isSidebarCollapsed;

    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<string> NavigationItems { get; } = ["Dashboard"];
    public ObservableCollection<CompanySelectionItem> CompanySelections { get; } = [];
    public ObservableCollection<GroupSelectionItem> GroupSelections { get; } = [];
    public ObservableCollection<LedgerInfo> Ledgers { get; } = [];
    public ObservableCollection<LedgerSelectionItem> LedgerSelections { get; } = [];
    public ObservableCollection<VoucherInfo> Vouchers { get; } = [];

    public string SelectedPage { get => _selectedPage; set { _selectedPage = value; OnChanged(); OnChanged(nameof(PageDescription)); } }
    public string PageDescription => SelectedPage == "Dashboard" ? "Read-only Tally extraction and ledger-wise Excel export." : "Configure and manage " + SelectedPage.ToLowerInvariant() + ".";
    public string Host { get => _host; set { _host = value; OnChanged(); } }
    public string Port { get => _port; set { _port = value; OnChanged(); } }
    public string ConnectionResult { get => _connectionResult; set { _connectionResult = value; OnChanged(); } }
    
    public string CompanySearch { get => _companySearch; set { _companySearch = value; OnChanged(); OnChanged(nameof(FilteredCompanySelections)); } }
    public string PlaceholderCompanyText { get => _placeholderCompanyText; set { _placeholderCompanyText = value; OnChanged(); } }
    public string SelectAllCompaniesText => CompanySelections.Count > 0 && CompanySelections.All(x => x.IsSelected) ? "Clear all" : "Select all";
    public IEnumerable<CompanySelectionItem> FilteredCompanySelections => CompanySelections.Where(x => string.IsNullOrWhiteSpace(CompanySearch) || x.Company.Name.Contains(CompanySearch, StringComparison.OrdinalIgnoreCase));

    public string GroupSearch { get => _groupSearch; set { _groupSearch = value; OnChanged(); OnChanged(nameof(FilteredGroupSelections)); } }
    public string PlaceholderGroupText { get => _placeholderGroupText; set { _placeholderGroupText = value; OnChanged(); } }
    public string SelectAllGroupsText => GroupSelections.Count > 0 && GroupSelections.All(x => x.IsSelected) ? "Clear all" : "Select all";
    public IEnumerable<GroupSelectionItem> FilteredGroupSelections => GroupSelections.Where(x => string.IsNullOrWhiteSpace(GroupSearch) || x.Group.Name.Contains(GroupSearch, StringComparison.OrdinalIgnoreCase));

    public string LedgerSearch { get => _ledgerSearch; set { _ledgerSearch = value; OnChanged(); OnChanged(nameof(FilteredLedgerSelections)); } }
    public string PlaceholderLedgerText { get => _placeholderLedgerText; set { _placeholderLedgerText = value; OnChanged(); } }
    public string SelectAllText => LedgerSelections.Count > 0 && LedgerSelections.All(x => x.IsSelected) ? "Clear all" : "Select all";
    public IEnumerable<LedgerSelectionItem> FilteredLedgerSelections => LedgerSelections.Where(x => string.IsNullOrWhiteSpace(LedgerSearch) || x.Ledger.Name.Contains(LedgerSearch, StringComparison.OrdinalIgnoreCase));

    private void OnCompanySelectionItemPropertyChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(CompanySelectionItem.IsSelected)) OnChanged(nameof(SelectAllCompaniesText)); }
    private void OnGroupSelectionItemPropertyChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(GroupSelectionItem.IsSelected)) OnChanged(nameof(SelectAllGroupsText)); }
    private void OnLedgerSelectionItemPropertyChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(LedgerSelectionItem.IsSelected)) OnChanged(nameof(SelectAllText)); }

    public DateTime? FromDate { get => _fromDate; set { _fromDate = value; OnChanged(); } }
    public DateTime? ToDate { get => _toDate; set { _toDate = value; OnChanged(); } }
    public string BatchDays { get => _batchDays; set { _batchDays = value; OnChanged(); } }
    public string ExportPath { get => _exportPath; set { _exportPath = value; OnChanged(); } }
    public string LastExportPath { get => _lastExportPath; private set { _lastExportPath = value; OnChanged(); } }
    public string ExtractionStatus { get => _extractionStatus; set { _extractionStatus = value; OnChanged(); } }
    public GridLength SidebarWidth => new(_isSidebarCollapsed ? 0 : 208);

    public ICommand TestConnectionCommand => new AsyncCommand(TestConnectionAsync);
    public ICommand LoadCompaniesCommand => new AsyncCommand(LoadCompaniesAsync);
    public ICommand LoadGroupsCommand => new AsyncCommand(LoadGroupsAsync);
    public ICommand LoadLedgersCommand => new AsyncCommand(LoadLedgersAsync);
    public ICommand SelectAllCompaniesCommand => new RelayCommand(SelectAllCompanies);
    public ICommand SelectAllGroupsCommand => new RelayCommand(SelectAllGroups);
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
            foreach (var item in CompanySelections) item.PropertyChanged -= OnCompanySelectionItemPropertyChanged;
            CompanySelections.Clear();
            foreach (var company in await companyService.GetCompaniesAsync(Profile(), _lifetimeCancellation.Token))
            {
                var item = new CompanySelectionItem(company);
                item.PropertyChanged += OnCompanySelectionItemPropertyChanged;
                CompanySelections.Add(item);
            }
            OnChanged(nameof(FilteredCompanySelections));
            OnChanged(nameof(SelectAllCompaniesText));
            ExtractionStatus = CompanySelections.Count == 0 ? "No companies were returned. Confirm that a company is loaded in TallyPrime." : $"{CompanySelections.Count} companies loaded. Select companies, then load groups.";
            
            PlaceholderCompanyText = CompanySelections.Count > 0 ? $"{CompanySelections.Count} companies loaded (e.g. {CompanySelections[0].Company.Name})" : "Search companies...";
        }
        catch (ConfigurationException exception) { ExtractionStatus = exception.Message; }
        catch (Exception exception) { ExtractionStatus = "Unable to load companies from TallyPrime: " + exception.Message; }
    }

    private async Task LoadGroupsAsync()
    {
        if (!await EnsureCompanyAsync()) return;

        try
        {
            foreach (var item in GroupSelections) item.PropertyChanged -= OnGroupSelectionItemPropertyChanged;
            GroupSelections.Clear();

            var selectedCompanies = CompanySelections.Where(x => x.IsSelected).Select(x => x.Company).ToList();
            var allGroups = new HashSet<string>();

            foreach (var company in selectedCompanies)
            {
                foreach (var group in await groupService.GetGroupsAsync(company, _lifetimeCancellation.Token))
                {
                    if (allGroups.Add(group.Name))
                    {
                        var item = new GroupSelectionItem(group);
                        item.PropertyChanged += OnGroupSelectionItemPropertyChanged;
                        GroupSelections.Add(item);
                    }
                }
            }

            OnChanged(nameof(FilteredGroupSelections));
            OnChanged(nameof(SelectAllGroupsText));
            ExtractionStatus = GroupSelections.Count == 0
                ? "No groups were returned across the selected companies."
                : $"{GroupSelections.Count} unique groups loaded. Select groups, then load ledgers.";
            
            PlaceholderGroupText = GroupSelections.Count > 0 ? $"{GroupSelections.Count} groups loaded (e.g. {GroupSelections[0].Group.Name})" : "Search groups...";
        }
        catch (Exception exception) { ExtractionStatus = "Unable to load groups from TallyPrime: " + exception.Message; }
    }

    private async Task LoadLedgersAsync()
    {
        if (!await EnsureCompanyAsync()) return;
        
        var selectedGroups = GroupSelections.Where(x => x.IsSelected).Select(x => x.Group).ToList();
        if (selectedGroups.Count == 0)
        {
            if (GroupSelections.Count == 0) await LoadGroupsAsync();
            selectedGroups = GroupSelections.Where(x => x.IsSelected).Select(x => x.Group).ToList();

            if (selectedGroups.Count == 0)
            {
                ExtractionStatus = "Select at least one group before loading ledgers.";
                return;
            }
        }

        try
        {
            foreach (var item in LedgerSelections) item.PropertyChanged -= OnLedgerSelectionItemPropertyChanged;
            Ledgers.Clear();
            LedgerSelections.Clear();

            var selectedCompanies = CompanySelections.Where(x => x.IsSelected).Select(x => x.Company).ToList();
            var allLedgers = new HashSet<string>();

            foreach (var company in selectedCompanies)
            {
                foreach (var group in selectedGroups)
                {
                    foreach (var ledger in await ledgerService.GetLedgersAsync(company, group.Name, _lifetimeCancellation.Token))
                    {
                        if (allLedgers.Add(ledger.Name))
                        {
                            Ledgers.Add(ledger);
                            var item = new LedgerSelectionItem(ledger);
                            item.PropertyChanged += OnLedgerSelectionItemPropertyChanged;
                            LedgerSelections.Add(item);
                        }
                    }
                }
            }

            OnChanged(nameof(FilteredLedgerSelections));
            OnChanged(nameof(SelectAllText));
            ExtractionStatus = LedgerSelections.Count == 0
                ? "No ledgers found for the selected groups."
                : $"{LedgerSelections.Count} unique ledgers loaded. Tick one or more for export.";

            PlaceholderLedgerText = LedgerSelections.Count > 0 ? $"{LedgerSelections.Count} ledgers loaded (e.g. {LedgerSelections[0].Ledger.Name})" : "Search ledgers...";
        }
        catch (Exception exception) { ExtractionStatus = "Unable to load ledgers from TallyPrime: " + exception.Message; }
    }

    private async Task<bool> EnsureCompanyAsync()
    {
        if (CompanySelections.Any(x => x.IsSelected)) return true;

        await LoadCompaniesAsync();
        if (CompanySelections.Any(x => x.IsSelected)) return true;

        ExtractionStatus = "No company is selected. Load companies, then select at least one.";
        return false;
    }

    private void SelectAllCompanies()
    {
        var targetState = !CompanySelections.All(x => x.IsSelected);
        foreach (var company in CompanySelections) company.IsSelected = targetState;
        ExtractionStatus = CompanySelections.Count == 0 ? "No companies are loaded to select." : $"All {CompanySelections.Count} loaded companies are {(targetState ? "selected" : "cleared")}.";
    }

    private void SelectAllGroups()
    {
        var targetState = !GroupSelections.All(x => x.IsSelected);
        foreach (var group in GroupSelections) group.IsSelected = targetState;
        ExtractionStatus = GroupSelections.Count == 0 ? "No groups are loaded to select." : $"All {GroupSelections.Count} loaded groups are {(targetState ? "selected" : "cleared")}.";
    }

    private void SelectAllLedgers()
    {
        var targetState = !LedgerSelections.All(x => x.IsSelected);
        foreach (var ledger in LedgerSelections)
        {
            ledger.IsSelected = targetState;
        }

        ExtractionStatus = LedgerSelections.Count == 0
            ? "No ledgers are loaded to select."
            : $"All {LedgerSelections.Count} loaded ledgers are {(targetState ? "selected" : "cleared")}.";
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
        var selectedCompanies = CompanySelections.Where(x => x.IsSelected).Select(x => x.Company).ToList();
        if (selectedCompanies.Count == 0) { ExtractionStatus = "No companies selected. Please select at least one company."; return; }
        if (FromDate is not { } fromDate || ToDate is not { } toDate) { ExtractionStatus = "Select both dates using the calendar."; return; }
        var from = DateOnly.FromDateTime(fromDate);
        var to = DateOnly.FromDateTime(toDate);
        if (from > to) { ExtractionStatus = "From Date must be on or before To Date."; return; }
        if (!int.TryParse(BatchDays, out var batchDays) || batchDays < 1) { ExtractionStatus = "Batch size must be at least one day."; return; }
        var selectedLedgers = LedgerSelections.Where(x => x.IsSelected).Select(x => x.Ledger).ToList();
        if (selectedLedgers.Count == 0) { ExtractionStatus = "No selected ledgers were provided."; return; }
        var selectedGroupNames = GroupSelections.Where(x => x.IsSelected).Select(x => x.Group.Name).FirstOrDefault();

        try
        {
            _extractionCancellation?.Dispose();
            _extractionCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
            var progress = new Progress<ExtractionProgress>(x => ExtractionStatus = $"{x.Stage} — batch {x.CurrentBatch}/{x.TotalBatches}; range {x.CurrentDateRange?.From:yyyy-MM-dd} to {x.CurrentDateRange?.To:yyyy-MM-dd}; vouchers {x.RecordsFound}; matched {x.MatchedTransactions}; {x.Percent}%");

            Vouchers.Clear();
            int totalVouchers = 0, totalTransactions = 0;
            string lastPath = ExportPath;

            foreach (var company in selectedCompanies)
            {
                var request = LedgerWiseExtractionRequest.Create(new CompanyContext(company, Profile()), DateRange.Create(from, to), selectedGroupNames, selectedLedgers, ExportPath, batchDays);
                var result = await exportService.ExtractAndExportAsync(request, progress, _extractionCancellation.Token);
                foreach (var voucher in result.Extraction.Ledgers.SelectMany(x => x.Transactions).Select(x => x.Voucher).DistinctBy(x => x.Guid ?? x.MasterId ?? x.SourceId)) Vouchers.Add(voucher);
                totalVouchers += result.Extraction.TotalVoucherCount;
                totalTransactions += result.Extraction.TotalMatchedTransactionCount;
                lastPath = result.OutputPath;
            }

            LastExportPath = lastPath;
            ExtractionStatus = $"Completed export for {selectedCompanies.Count} company/companies. {totalVouchers} vouchers, {totalTransactions} ledger transactions. Workbook: {lastPath}";
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
