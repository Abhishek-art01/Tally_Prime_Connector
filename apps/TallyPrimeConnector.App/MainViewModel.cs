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

public sealed record PreviewRow(string Group, string Ledger, DateOnly Date, string VoucherType, string VoucherNumber, string? Party, string? Narration, decimal Debit, decimal Credit);

public sealed class MainViewModel(IConnectionService connectionService, ICompanyService companyService, IGroupService groupService, ILedgerService ledgerService, ILedgerWiseExtractionService extractionService, ILedgerWiseExportService exportService) : INotifyPropertyChanged
{
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private CancellationTokenSource? _extractionCancellation;
    private string _selectedPage = "Dashboard", _host = "localhost", _port = "9000", _connectionResult = "No connection test has been run.", _ledgerSearch = "", _placeholderLedgerText = "Search ledgers...", _batchDays = LedgerWiseExtractionRequest.DefaultBatchDays.ToString(), _extractionStatus = "Select a company, dates, one or more ledgers, and an output file.";
    private string _companySearch = "", _placeholderCompanyText = "Search companies...", _groupSearch = "", _placeholderGroupText = "Search groups...";
    private DateTime? _fromDate = DateTime.Today, _toDate = DateTime.Today;
    private bool _isSidebarCollapsed;

    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<string> NavigationItems { get; } = ["Dashboard", "Settings"];
    public ObservableCollection<CompanySelectionItem> CompanySelections { get; } = [];
    public ObservableCollection<GroupSelectionItem> GroupSelections { get; } = [];
    public ObservableCollection<LedgerInfo> Ledgers { get; } = [];
    public ObservableCollection<LedgerSelectionItem> LedgerSelections { get; } = [];
    public ObservableCollection<PreviewRow> PreviewRows { get; } = [];

    public string SelectedPage { get => _selectedPage; set { _selectedPage = value; OnChanged(); OnChanged(nameof(PageDescription)); } }
    public string PageDescription => SelectedPage == "Dashboard" ? "Read-only Tally extraction and ledger-wise Excel export." : "Configure and manage " + SelectedPage.ToLowerInvariant() + ".";
    public string Host { get => _host; set { _host = value; OnChanged(); } }
    public string Port { get => _port; set { _port = value; OnChanged(); } }
    public string ConnectionResult { get => _connectionResult; set { _connectionResult = value; OnChanged(); } }
    
    public string CompanySearch { get => _companySearch; set { _companySearch = value; OnChanged(); OnChanged(nameof(FilteredCompanySelections)); } }
    public string PlaceholderCompanyText { get => _placeholderCompanyText; set { _placeholderCompanyText = value; OnChanged(); } }
    public CompanyInfo? SelectedCompany => CompanySelections.FirstOrDefault(x => x.IsSelected)?.Company;
    public IEnumerable<CompanySelectionItem> FilteredCompanySelections => CompanySelections.Where(x => string.IsNullOrWhiteSpace(CompanySearch) || x.Company.Name.Contains(CompanySearch, StringComparison.OrdinalIgnoreCase));

    public string GroupSearch { get => _groupSearch; set { _groupSearch = value; OnChanged(); OnChanged(nameof(FilteredGroupSelections)); } }
    public string PlaceholderGroupText { get => _placeholderGroupText; set { _placeholderGroupText = value; OnChanged(); } }
    public string SelectAllGroupsText => GroupSelections.Count > 0 && GroupSelections.All(x => x.IsSelected) ? "Clear all" : "Select all";
    public IEnumerable<GroupSelectionItem> FilteredGroupSelections => GroupSelections.Where(x => string.IsNullOrWhiteSpace(GroupSearch) || x.Group.Name.Contains(GroupSearch, StringComparison.OrdinalIgnoreCase));

    public string LedgerSearch { get => _ledgerSearch; set { _ledgerSearch = value; OnChanged(); OnChanged(nameof(FilteredLedgerSelections)); } }
    public string PlaceholderLedgerText { get => _placeholderLedgerText; set { _placeholderLedgerText = value; OnChanged(); } }
    public string SelectAllText => LedgerSelections.Count > 0 && LedgerSelections.All(x => x.IsSelected) ? "Clear all" : "Select all";
    public int SelectedLedgerCount => LedgerSelections.Count(x => x.IsSelected);
    public IEnumerable<LedgerSelectionItem> FilteredLedgerSelections => LedgerSelections.Where(x => string.IsNullOrWhiteSpace(LedgerSearch) || x.Ledger.Name.Contains(LedgerSearch, StringComparison.OrdinalIgnoreCase));

    private bool _updatingCompanySelection;
    private void OnCompanySelectionItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CompanySelectionItem.IsSelected) || _updatingCompanySelection) return;
        if (sender is CompanySelectionItem selected && selected.IsSelected)
        {
            _updatingCompanySelection = true;
            foreach (var item in CompanySelections)
                if (!ReferenceEquals(item, selected)) item.IsSelected = false;
            _updatingCompanySelection = false;
        }
        // Update placeholder to reflect selection
        var sel = SelectedCompany;
        PlaceholderCompanyText = sel != null ? sel.Name : (CompanySelections.Count > 0 ? $"{CompanySelections.Count} companies loaded" : "Search companies...");
    }
    private void OnGroupSelectionItemPropertyChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(GroupSelectionItem.IsSelected)) OnChanged(nameof(SelectAllGroupsText)); }
    private void OnLedgerSelectionItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LedgerSelectionItem.IsSelected))
        {
            OnChanged(nameof(SelectAllText));
            OnChanged(nameof(SelectedLedgerCount));
        }
    }

    public DateTime? FromDate { get => _fromDate; set { _fromDate = value; OnChanged(); } }
    public DateTime? ToDate { get => _toDate; set { _toDate = value; OnChanged(); } }
    public string BatchDays { get => _batchDays; set { _batchDays = value; OnChanged(); } }
    public string ExtractionStatus { get => _extractionStatus; set { _extractionStatus = value; OnChanged(); } }
    public string PreviewHeader => PreviewRows.Count > 0 ? $"Preview ({PreviewRows.Count} rows)" : "Preview";
    public bool IsPreviewExpanded => PreviewRows.Count > 0;
    public GridLength SidebarWidth => new(_isSidebarCollapsed ? 0 : 208);

    public ICommand TestConnectionCommand => new AsyncCommand(TestConnectionAsync);
    public ICommand LoadCompaniesCommand => new AsyncCommand(LoadCompaniesAsync);
    public ICommand LoadGroupsCommand => new AsyncCommand(LoadGroupsAsync);
    public ICommand LoadLedgersCommand => new AsyncCommand(LoadLedgersAsync);
    public ICommand SelectAllGroupsCommand => new RelayCommand(SelectAllGroups);
    public ICommand SelectAllLedgersCommand => new RelayCommand(SelectAllLedgers);
    public ICommand LoadPreviewCommand => new AsyncCommand(LoadPreviewAsync);
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

            foreach (var group in await groupService.GetGroupsAsync(SelectedCompany!, _lifetimeCancellation.Token))
            {
                var item = new GroupSelectionItem(group);
                item.PropertyChanged += OnGroupSelectionItemPropertyChanged;
                GroupSelections.Add(item);
            }

            OnChanged(nameof(FilteredGroupSelections));
            OnChanged(nameof(SelectAllGroupsText));
            ExtractionStatus = GroupSelections.Count == 0
                ? "No groups were returned for the selected company."
                : $"{GroupSelections.Count} groups loaded. Select groups, then load ledgers.";

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

            var allLedgers = new HashSet<string>();
            foreach (var group in selectedGroups)
            {
                foreach (var ledger in await ledgerService.GetLedgersAsync(SelectedCompany!, group.Name, _lifetimeCancellation.Token))
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
        if (SelectedCompany is not null) return true;

        await LoadCompaniesAsync();
        if (SelectedCompany is not null) return true;

        ExtractionStatus = "No company is selected. Load companies, then select one.";
        return false;
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
        foreach (var ledger in LedgerSelections) ledger.IsSelected = targetState;
        ExtractionStatus = LedgerSelections.Count == 0
            ? "No ledgers are loaded to select."
            : $"All {LedgerSelections.Count} loaded ledgers are {(targetState ? "selected" : "cleared")}.";
    }

    private async Task LoadPreviewAsync()
    {
        if (SelectedCompany is null) { ExtractionStatus = "No company selected."; return; }
        if (FromDate is not { } fromDate || ToDate is not { } toDate) { ExtractionStatus = "Select both dates using the calendar."; return; }
        var from = DateOnly.FromDateTime(fromDate);
        var to = DateOnly.FromDateTime(toDate);
        if (from > to) { ExtractionStatus = "From Date must be on or before To Date."; return; }
        if (!int.TryParse(BatchDays, out var batchDays) || batchDays < 1) { ExtractionStatus = "Batch size must be at least one day."; return; }
        var selectedLedgers = LedgerSelections.Where(x => x.IsSelected).Select(x => x.Ledger).ToList();
        if (selectedLedgers.Count == 0) { ExtractionStatus = "No ledgers selected for preview."; return; }
        var selectedGroupName = GroupSelections.FirstOrDefault(x => x.IsSelected)?.Group.Name;

        try
        {
            _extractionCancellation?.Dispose();
            _extractionCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
            var progress = new Progress<ExtractionProgress>(x => ExtractionStatus = $"Preview — batch {x.CurrentBatch}/{x.TotalBatches}; {x.Percent}%");
            var request = LedgerWiseExtractionRequest.Create(new CompanyContext(SelectedCompany, Profile()), DateRange.Create(from, to), selectedGroupName, selectedLedgers, Path.GetTempFileName(), batchDays);
            var result = await extractionService.ExtractAsync(request, progress, _extractionCancellation.Token);
            PopulatePreviewRows(result);
            ExtractionStatus = $"Preview loaded: {result.TotalMatchedTransactionCount} transactions across {selectedLedgers.Count} ledger(s).";
        }
        catch (OperationCanceledException) { ExtractionStatus = "Preview cancelled."; }
        catch (Exception ex) { ExtractionStatus = "Preview failed: " + ex.Message; }
    }

    private void PopulatePreviewRows(LedgerWiseExtractionResult result)
    {
        PreviewRows.Clear();
        foreach (var ledgerResult in result.Ledgers)
        {
            var groupName = ledgerResult.Ledger.GroupName ?? "-";
            var ledgerName = ledgerResult.Ledger.Name;
            foreach (var tx in ledgerResult.Transactions)
            {
                var debit  = tx.Entry.Amount.Direction == DebitCredit.Debit  ? tx.Entry.Amount.Value : 0m;
                var credit = tx.Entry.Amount.Direction == DebitCredit.Credit ? tx.Entry.Amount.Value : 0m;
                PreviewRows.Add(new PreviewRow(groupName, ledgerName, tx.Voucher.Date, tx.Voucher.VoucherType ?? "", tx.Voucher.VoucherNumber ?? "", tx.Voucher.PartyLedgerName ?? tx.Voucher.Party?.Name, tx.Voucher.Narration, debit, credit));
            }
        }
        OnChanged(nameof(PreviewHeader));
        OnChanged(nameof(IsPreviewExpanded));
    }

    private async Task ExtractAndExportAsync()
    {
        if (SelectedCompany is null) { ExtractionStatus = "No company selected. Please select a company first."; return; }
        if (FromDate is not { } fromDate || ToDate is not { } toDate) { ExtractionStatus = "Select both dates using the calendar."; return; }
        var from = DateOnly.FromDateTime(fromDate);
        var to = DateOnly.FromDateTime(toDate);
        if (from > to) { ExtractionStatus = "From Date must be on or before To Date."; return; }
        if (!int.TryParse(BatchDays, out var batchDays) || batchDays < 1) { ExtractionStatus = "Batch size must be at least one day."; return; }
        var selectedLedgers = LedgerSelections.Where(x => x.IsSelected).Select(x => x.Ledger).ToList();
        if (selectedLedgers.Count == 0) { ExtractionStatus = "No selected ledgers were provided."; return; }
        var selectedGroupName = GroupSelections.FirstOrDefault(x => x.IsSelected)?.Group.Name;

        // Ask user where to save before starting extraction
        var safeName = string.Concat(SelectedCompany.Name.Split(Path.GetInvalidFileNameChars()));
        var autoFileName = $"{safeName}_{from:yyyy-MM-dd}_{to:yyyy-MM-dd}.xlsx";
        var dialog = new SaveFileDialog
        {
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            AddExtension = true,
            FileName = autoFileName,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog() != true) { ExtractionStatus = "Export cancelled — no file was selected."; return; }
        var outputPath = dialog.FileName;

        try
        {
            _extractionCancellation?.Dispose();
            _extractionCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
            var progress = new Progress<ExtractionProgress>(x => ExtractionStatus = $"{x.Stage} — batch {x.CurrentBatch}/{x.TotalBatches}; range {x.CurrentDateRange?.From:yyyy-MM-dd} to {x.CurrentDateRange?.To:yyyy-MM-dd}; vouchers {x.RecordsFound}; matched {x.MatchedTransactions}; {x.Percent}%");

            var request = LedgerWiseExtractionRequest.Create(new CompanyContext(SelectedCompany, Profile()), DateRange.Create(from, to), selectedGroupName, selectedLedgers, outputPath, batchDays);
            var result = await exportService.ExtractAndExportAsync(request, progress, _extractionCancellation.Token);
            PopulatePreviewRows(result.Extraction);
            ExtractionStatus = $"Completed export for {SelectedCompany.Name}. {result.Extraction.TotalVoucherCount} vouchers, {result.Extraction.TotalMatchedTransactionCount} ledger transactions. Saved: {result.OutputPath}";
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
