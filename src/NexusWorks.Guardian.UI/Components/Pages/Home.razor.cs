using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using NexusWorks.Guardian.Baseline;
using NexusWorks.Guardian.Models;
using NexusWorks.Guardian.Preferences;
using NexusWorks.Guardian.UI.Services;

namespace NexusWorks.Guardian.UI.Components.Pages;

public partial class Home : IDisposable, IAsyncDisposable
{
    [Inject] private IPathSelectionService PathSelector { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private GuardianWorkbenchService Workbench { get; set; } = default!;
    [Inject] private ISftpSecretStore SecretStore { get; set; } = default!;

    private readonly RunFormModel _runForm = new();
    private readonly HashSet<string> _bulkSelectedPaths = new(StringComparer.OrdinalIgnoreCase);
    private ExecutionReport? _report;
    private IReadOnlyList<ExecutionHistoryEntry> _history = Array.Empty<ExecutionHistoryEntry>();
    private IReadOnlyList<string> _recentCurrentRoots = Array.Empty<string>();
    private IReadOnlyList<string> _recentPatchRoots = Array.Empty<string>();
    private IReadOnlyList<string> _recentBaselinePaths = Array.Empty<string>();
    private IReadOnlyList<string> _recentOutputRoots = Array.Empty<string>();
    private BaselinePreviewSummary? _baselinePreview;
    private SampleDatasetInfo? _sampleDataset;
    private string? _baselinePreviewError;
    private string? _errorMessage;
    private string? _noticeMessage;
    private UiNoticeKind _noticeKind;
    private CancellationTokenSource? _noticeAutoDismissCts;
    private DotNetObjectReference<Home>? _hotkeyReference;
    private bool _hotkeysRegistered;
    private bool _showShortcutOverlay;
    private bool _isCompactInspectorOpen;
    private bool _pendingSearchFocus;
    private string? _pendingDeleteExecutionId;
    private string? SelectedHistoryExecutionId { get; set; }
    private string _searchText = string.Empty;
    private ResultFilter _selectedFilter = ResultFilter.All;
    private bool _isRunning;
    private string? SelectedRelativePath { get; set; }

    private bool HasReport => _report is not null;
    private bool HasSampleDataset => _sampleDataset is not null;
    private bool CanOpenCurrentRoot => CanOpenDirectory(_runForm.CurrentRootPath);
    private bool CanOpenPatchRoot => CanOpenDirectory(_runForm.PatchRootPath);
    private bool CanOpenBaselinePath => CanOpenFile(_runForm.BaselinePath);
    private bool CanOpenOutputRoot => CanOpenDirectory(_runForm.OutputRootPath);
    private bool CanOpenReportHtml => HasReport && CanOpenFile(_report!.Artifacts.HtmlReportPath);
    private bool CanOpenReportExcel => HasReport && CanOpenFile(_report!.Artifacts.ExcelReportPath);
    private bool CanOpenReportJson => HasReport && CanOpenFile(_report!.Artifacts.JsonResultPath);
    private bool CanOpenReportLog => HasReport && CanOpenFile(_report!.Artifacts.LogPath);
    private bool CanOpenReportOutputDirectory => HasReport && CanOpenDirectory(_report!.Artifacts.OutputDirectory);
    private string? SelectedCurrentArtifactPath => ResolveSelectedArtifactPath(_report?.Request.CurrentRootPath, SelectedItem, isCurrent: true);
    private string? SelectedPatchArtifactPath => ResolveSelectedArtifactPath(_report?.Request.PatchRootPath, SelectedItem, isCurrent: false);
    private bool CanOpenSelectedCurrentArtifact => CanOpenFile(SelectedCurrentArtifactPath);
    private bool CanOpenSelectedPatchArtifact => CanOpenFile(SelectedPatchArtifactPath);
    private bool IsCurrentSftp => _runForm.CurrentMode == InputSourceMode.Sftp;
    private bool IsPatchSftp => _runForm.PatchMode == InputSourceMode.Sftp;
    private bool CurrentInputConfigReady => EvaluateSftpConfigReady(
        _runForm.CurrentMode,
        _runForm.CurrentSftpHost,
        _runForm.CurrentSftpUsername,
        _runForm.CurrentSftpAuthenticationMode,
        _runForm.CurrentSftpPassword,
        _runForm.CurrentSftpPrivateKeyPath,
        _runForm.CurrentSftpRemoteRoot);
    private bool PatchInputConfigReady => EvaluatePatchSftpConfigReady(_runForm);
    private bool CanToggleCompactInspector => HasReport && (SelectedItem is not null || _isCompactInspectorOpen);
    private string CompactResultsPanelClass => _isCompactInspectorOpen
        ? "gw-home-results gw-panel hidden min-h-0 flex-col overflow-hidden xl:flex"
        : "gw-home-results gw-panel flex min-h-0 flex-col overflow-hidden";
    private string CompactInspectorPanelClass => _isCompactInspectorOpen
        ? "gw-home-inspector gw-panel flex min-h-[24rem] flex-col overflow-hidden xl:hidden"
        : "gw-home-inspector gw-panel hidden min-h-[24rem] flex-col overflow-hidden xl:hidden";
    private string CompactQueueToggleClass => !_isCompactInspectorOpen ? "gw-chip-active" : "gw-chip";
    private string CompactDetailsToggleClass => _isCompactInspectorOpen ? "gw-chip-active" : "gw-chip";
    private IReadOnlyList<ComparisonItemResult> BulkSelectedItems => _report?.Result.Items
        .Where(item => _bulkSelectedPaths.Contains(item.RelativePath))
        .ToArray() ?? Array.Empty<ComparisonItemResult>();
    private bool CanBulkOpenCurrent => ResolveBulkArtifactPaths(isCurrent: true).Count > 0;
    private bool CanBulkOpenPatch => ResolveBulkArtifactPaths(isCurrent: false).Count > 0;
    private PathStatusBadge CurrentRootBadge => EvaluateInputRootBadge(_runForm.CurrentRootPath, _runForm.CurrentMode);
    private PathStatusBadge PatchRootBadge => EvaluateInputRootBadge(_runForm.PatchRootPath, _runForm.PatchMode);
    private PathStatusBadge OutputRootBadge => EvaluateOutputDirectoryBadge(_runForm.OutputRootPath);
    private PathStatusBadge BaselinePathBadge => EvaluateBaselineBadge(_runForm.BaselinePath, _baselinePreviewError);
    private bool CanRunCompare => !_isRunning
        && !string.IsNullOrWhiteSpace(_runForm.ReportTitle)
        && CurrentRootBadge.CanProceed
        && PatchRootBadge.CanProceed
        && CurrentInputConfigReady
        && PatchInputConfigReady
        && OutputRootBadge.CanProceed
        && BaselinePathBadge.CanProceed
        && _baselinePreview is not null;
    private RunReadinessStatus RunReadiness => EvaluateRunReadiness(
        _runForm.ReportTitle,
        CurrentRootBadge,
        PatchRootBadge,
        CurrentInputConfigReady,
        PatchInputConfigReady,
        BaselinePathBadge,
        OutputRootBadge,
        _baselinePreview);
    private string NoticeBannerClass => _noticeKind switch
    {
        UiNoticeKind.Success => "border-emerald-200 bg-emerald-50 text-emerald-700",
        _ => "border-blue-200 bg-blue-50 text-blue-700",
    };
    private string NoticeIcon => _noticeKind switch
    {
        UiNoticeKind.Success => "task_alt",
        _ => "info",
    };
    private static TimeSpan NoticeAutoDismissDelay => TimeSpan.FromSeconds(5);
    private IReadOnlyList<ShortcutEntry> ShortcutEntries =>
    [
        new(["Ctrl/Cmd", "Enter"], "Run compare", "Starts a new compare run with the current form values when all readiness checks pass."),
        new(["Ctrl/Cmd", "Shift", "Enter"], "Rerun last setup", "Runs the compare flow again using the current inputs after at least one execution has completed."),
        new(["Alt", "Shift", "H"], "Refresh history", "Reloads recent execution history from the selected output root."),
        new(["Alt", "Shift", "S"], "Load sample dataset", "Fills the form with the bundled sample dataset when it exists in the workspace."),
        new(["[", "]"], "Move history selection", "Moves the current selection through the execution history list without using the mouse."),
        new(["M"], "Load selected history", "Loads the currently selected execution history entry into the review console."),
        new(["/"], "Focus search", "Moves keyboard focus to the result search box and, in compact layouts, returns from Details to Queue first."),
        new(["1", "2", "3", "4", "5"], "Switch status filters", "Applies All, Changed, Missing Required, Error, or OK filters to the result list without leaving the keyboard."),
        new(["J", "K"], "Move selection", "Moves the current result selection down or up through the filtered comparison list."),
        new(["N", "P"], "Alternative navigation", "Uses next and previous keys to move through filtered results without leaving the keyboard."),
        new(["X"], "Toggle selected row", "Adds or removes the currently selected result row from the bulk-selection set."),
        new(["Alt", "Shift", "A"], "Select visible rows", "Adds every currently visible result row to the bulk-selection set."),
        new(["Alt", "Shift", "R"], "Select review set", "Adds all visible non-OK rows to the bulk-selection set."),
        new(["Alt", "Shift", "C"], "Clear bulk selection", "Removes every row from the current bulk-selection set."),
        new(["O"], "Open current file", "Opens the current-side file for the selected comparison item when a real source file exists."),
        new(["Shift", "O"], "Open patch file", "Opens the patch-side file for the selected comparison item when a real source file exists."),
        new(["Alt", "Shift", "O"], "Open bulk current", "Opens every current-side file in the current bulk-selection set."),
        new(["Alt", "Shift", "P"], "Open bulk patch", "Opens every patch-side file in the current bulk-selection set."),
        new(["H", "E", "D", "L", "U"], "Open current run artifacts", "Opens the HTML report, Excel report, JSON results, execution log, or output directory for the current run."),
        new(["?"], "Open shortcut help", "Opens this reference panel from anywhere in the execution screen."),
        new(["Esc"], "Dismiss active panel", "Closes the shortcut panel or hides the compact Details panel without changing the current selection."),
    ];

    private IReadOnlyList<SummaryCard> SummaryCards =>
    [
        new("Files scanned", GetCount("Total"), "Inventory", "bg-slate-100 text-slate-700 ring-1 ring-inset ring-slate-200", "Total result items in this execution."),
        new("Changed items", GetCount(CompareStatus.Changed.ToString()), "Changed", "bg-amber-100 text-amber-700 ring-1 ring-inset ring-amber-200", "Items with meaningful differences after configured compare rules."),
        new("Critical issues", GetCriticalCount(), "Critical", "bg-red-100 text-red-700 ring-1 ring-inset ring-red-200", "Missing required files and explicit errors block approval."),
        new("Artifacts", HasReport ? "4" : "0", "Ready", "bg-blue-100 text-blue-700 ring-1 ring-inset ring-blue-200", "HTML, Excel, JSON, and execution log are written per run."),
    ];

    private IReadOnlyList<ResultFilter> Filters =>
    [
        ResultFilter.All,
        ResultFilter.Changed,
        ResultFilter.MissingRequired,
        ResultFilter.Error,
        ResultFilter.Ok,
    ];

    private IReadOnlyList<ComparisonItemResult> FilteredItems => _report?.Result.Items
        .Where(MatchesFilter)
        .Where(MatchesSearch)
        .OrderByDescending(GetSeverityWeight)
        .ThenBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
        .ToArray() ?? Array.Empty<ComparisonItemResult>();

    private IReadOnlyList<ExecutionStageMetric> PerformanceStages => _report?.Summary.StageMetrics ?? Array.Empty<ExecutionStageMetric>();

    private ComparisonItemResult? SelectedItem => FilteredItems.FirstOrDefault(item => item.RelativePath == SelectedRelativePath)
        ?? FilteredItems.FirstOrDefault();
    private ExecutionHistoryEntry? SelectedHistoryEntry => _history.FirstOrDefault(entry => string.Equals(entry.ExecutionId, SelectedHistoryExecutionId, StringComparison.OrdinalIgnoreCase))
        ?? _history.FirstOrDefault();

    protected override async Task OnInitializedAsync()
    {
        _sampleDataset = TryLocateSampleDataset();
        _report = Workbench.LastReport;
        if (_report is not null)
        {
            SelectedRelativePath = _report.Result.Items.FirstOrDefault()?.RelativePath;
        }

        await LoadStoredSftpSecretsAsync();
        await RefreshRecentPathsAsync();
        await RefreshBaselinePreviewAsync();
        await RefreshHistoryAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                _hotkeyReference = DotNetObjectReference.Create(this);
                await JSRuntime.InvokeVoidAsync("guardianHotkeys.register", _hotkeyReference);
                _hotkeysRegistered = true;
            }
            catch (JSException)
            {
                _hotkeyReference?.Dispose();
                _hotkeyReference = null;
            }
        }

        if (!_pendingSearchFocus)
        {
            return;
        }

        _pendingSearchFocus = false;

        try
        {
            await JSRuntime.InvokeVoidAsync("guardianHotkeys.focusResultSearch");
        }
        catch (JSException)
        {
        }
    }

    [JSInvokable]
    public async Task HandleHotkeyAsync(string action)
    {
        if (_showShortcutOverlay)
        {
            switch (action)
            {
                case "toggle-shortcuts":
                case "hide-shortcuts":
                    HideShortcutOverlay();
                    break;
            }

            return;
        }

        switch (action)
        {
            case "run":
                await RunCompareAsync();
                break;
            case "rerun":
                await HandleRerunHotkeyAsync();
                break;
            case "refresh-history":
                await HandleRefreshHistoryHotkeyAsync();
                break;
            case "load-sample":
                await HandleLoadSampleHotkeyAsync();
                break;
            case "select-history-previous":
                await MoveHistorySelectionAsync(direction: -1);
                break;
            case "select-history-next":
                await MoveHistorySelectionAsync(direction: 1);
                break;
            case "load-selected-history":
                await HandleLoadSelectedHistoryHotkeyAsync();
                break;
            case "focus-search":
                await FocusSearchAsync();
                break;
            case "select-filter-all":
                HandleFilterHotkey(ResultFilter.All);
                break;
            case "select-filter-changed":
                HandleFilterHotkey(ResultFilter.Changed);
                break;
            case "select-filter-missing-required":
                HandleFilterHotkey(ResultFilter.MissingRequired);
                break;
            case "select-filter-error":
                HandleFilterHotkey(ResultFilter.Error);
                break;
            case "select-filter-ok":
                HandleFilterHotkey(ResultFilter.Ok);
                break;
            case "select-next":
                await MoveSelectionAsync(direction: 1);
                break;
            case "select-previous":
                await MoveSelectionAsync(direction: -1);
                break;
            case "toggle-selected-row":
                HandleToggleSelectedRowHotkey();
                break;
            case "select-visible":
                HandleSelectVisibleHotkey();
                break;
            case "select-review-set":
                HandleSelectReviewSetHotkey();
                break;
            case "clear-bulk-selection":
                HandleClearBulkSelectionHotkey();
                break;
            case "open-selected-current":
                await HandleOpenSelectedArtifactHotkeyAsync(isCurrent: true);
                break;
            case "open-selected-patch":
                await HandleOpenSelectedArtifactHotkeyAsync(isCurrent: false);
                break;
            case "open-bulk-current":
                await HandleOpenBulkArtifactsHotkeyAsync(isCurrent: true);
                break;
            case "open-bulk-patch":
                await HandleOpenBulkArtifactsHotkeyAsync(isCurrent: false);
                break;
            case "open-report-html":
                await HandleOpenReportArtifactHotkeyAsync(ReportArtifactShortcut.Html);
                break;
            case "open-report-excel":
                await HandleOpenReportArtifactHotkeyAsync(ReportArtifactShortcut.Excel);
                break;
            case "open-report-json":
                await HandleOpenReportArtifactHotkeyAsync(ReportArtifactShortcut.Json);
                break;
            case "open-report-log":
                await HandleOpenReportArtifactHotkeyAsync(ReportArtifactShortcut.Log);
                break;
            case "open-report-output":
                await HandleOpenReportArtifactHotkeyAsync(ReportArtifactShortcut.OutputDirectory);
                break;
            case "toggle-shortcuts":
                ShowShortcutOverlay();
                break;
            case "hide-shortcuts":
                if (_isCompactInspectorOpen)
                {
                    HideCompactInspector();
                }
                break;
        }
    }

    private async Task RunCompareAsync()
    {
        if (!CanRunCompare)
        {
            SetError(RunReadiness.Description);
            return;
        }

        ClearMessages();
        _isRunning = true;

        try
        {
            await PersistSftpSecretsAsync();
            _report = await Workbench.RunAsync(
                new GuardianRunRequest(
                    BuildCurrentInputRequest(),
                    BuildPatchInputRequest(),
                    _runForm.BaselinePath,
                    _runForm.OutputRootPath,
                    _runForm.ReportTitle));

            ApplyReport(_report);
            await RefreshRecentPathsAsync();
            await RefreshHistoryAsync();
            SetSuccessNotice($"Inspection completed. {_report.Summary.TotalFileCount} items processed in {FormatDuration(_report.Summary.TotalDurationMs)}. Execution {_report.Summary.ExecutionId} is ready.");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            _isRunning = false;
        }
    }

    private Task RerunAsync() => HasReport ? RunCompareAsync() : Task.CompletedTask;

    private async Task HandleRerunHotkeyAsync()
    {
        if (_isRunning)
        {
            SetInfoNotice("A compare run is already in progress.");
            return;
        }

        if (!HasReport)
        {
            SetInfoNotice("Rerun is available after the first completed execution.");
            return;
        }

        if (!CanRunCompare)
        {
            SetError(RunReadiness.Description);
            return;
        }

        await RerunAsync();
    }

    private async Task RefreshHistoryAsync()
    {
        if (string.IsNullOrWhiteSpace(_runForm.OutputRootPath))
        {
            _history = Array.Empty<ExecutionHistoryEntry>();
            _pendingDeleteExecutionId = null;
            SelectedHistoryExecutionId = null;
            return;
        }

        _history = await Workbench.ListRecentAsync(_runForm.OutputRootPath);
        SyncSelectedHistoryEntry();
        if (!string.IsNullOrWhiteSpace(_pendingDeleteExecutionId)
            && !_history.Any(entry => string.Equals(entry.ExecutionId, _pendingDeleteExecutionId, StringComparison.OrdinalIgnoreCase)))
        {
            _pendingDeleteExecutionId = null;
        }
    }

    private async Task RefreshHistoryFromToolbarAsync()
    {
        try
        {
            await RefreshHistoryAsync();

            if (string.IsNullOrWhiteSpace(_runForm.OutputRootPath))
            {
                SetInfoNotice("Execution history is unavailable until an output root is set.");
                return;
            }

            SetInfoNotice(_history.Count == 0
                ? "No execution history was found in the selected output root."
                : $"Loaded {_history.Count} recent execution {(_history.Count == 1 ? "entry" : "entries")} from history.");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }

    private async Task HandleRefreshHistoryHotkeyAsync()
    {
        if (_isRunning)
        {
            SetInfoNotice("Execution history cannot refresh while a compare run is in progress.");
            return;
        }

        await RefreshHistoryFromToolbarAsync();
    }

    private async Task RefreshRecentPathsAsync()
    {
        var currentTask = Workbench.ListRecentPathsAsync(RecentPathKind.CurrentRoot);
        var patchTask = Workbench.ListRecentPathsAsync(RecentPathKind.PatchRoot);
        var baselineTask = Workbench.ListRecentPathsAsync(RecentPathKind.BaselineFile);
        var outputTask = Workbench.ListRecentPathsAsync(RecentPathKind.OutputRoot);

        await Task.WhenAll(currentTask, patchTask, baselineTask, outputTask);

        _recentCurrentRoots = currentTask.Result;
        _recentPatchRoots = patchTask.Result;
        _recentBaselinePaths = baselineTask.Result;
        _recentOutputRoots = outputTask.Result;
    }

    private async Task RefreshBaselinePreviewAsync()
    {
        _baselinePreview = null;
        _baselinePreviewError = null;

        if (string.IsNullOrWhiteSpace(_runForm.BaselinePath) || !File.Exists(_runForm.BaselinePath))
        {
            return;
        }

        try
        {
            _baselinePreview = await Workbench.PreviewBaselineAsync(_runForm.BaselinePath);
        }
        catch (Exception ex)
        {
            _baselinePreviewError = ex.Message;
        }
    }

    private async Task LoadHistoryAsync(string jsonResultPath)
    {
        try
        {
            var report = await Workbench.LoadAsync(jsonResultPath);
            if (report is null)
            {
                SetError("Failed to load the selected execution history entry.");
                return;
            }

            _pendingDeleteExecutionId = null;
            ApplyReport(report);
            SelectedHistoryExecutionId = report.Summary.ExecutionId;
            SetInfoNotice($"Loaded execution {report.Summary.ExecutionId} into the review console.");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }

    private void ArmDeleteHistory(string executionId)
        => _pendingDeleteExecutionId = executionId;

    private void CancelDeleteHistory()
        => _pendingDeleteExecutionId = null;

    private void SelectHistoryEntry(string executionId)
        => SelectedHistoryExecutionId = executionId;

    private async Task DeleteHistoryAsync(ExecutionHistoryEntry entry)
    {
        try
        {
            ClearError();

            var deleted = await Workbench.DeleteHistoryAsync(entry.OutputDirectory);
            if (!deleted)
            {
                SetError("The selected execution history entry could not be deleted.");
                return;
            }

            if (string.Equals(_report?.Artifacts.OutputDirectory, entry.OutputDirectory, StringComparison.OrdinalIgnoreCase))
            {
                _report = null;
                SelectedRelativePath = null;
                _bulkSelectedPaths.Clear();
            }

            _pendingDeleteExecutionId = null;
            await RefreshHistoryAsync();
            SetSuccessNotice($"Deleted execution {entry.ExecutionId} from history.");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }

    private async Task LoadSampleDatasetAsync()
    {
        if (_sampleDataset is null)
        {
            SetError("Sample dataset was not found in this workspace.");
            return;
        }

        ClearError();
        _runForm.ReportTitle = "Guardian Sample Inspection";
        _runForm.CurrentMode = InputSourceMode.Local;
        _runForm.PatchMode = InputSourceMode.Local;
        _runForm.PatchUseCurrentSftpConnection = true;
        ClearSftpFields();
        _runForm.CurrentRootPath = _sampleDataset.CurrentRootPath;
        _runForm.PatchRootPath = _sampleDataset.PatchRootPath;
        _runForm.BaselinePath = _sampleDataset.BaselinePath;
        _runForm.OutputRootPath = _sampleDataset.OutputRootPath;

        await Workbench.RememberPathAsync(RecentPathKind.CurrentRoot, _sampleDataset.CurrentRootPath);
        await Workbench.RememberPathAsync(RecentPathKind.PatchRoot, _sampleDataset.PatchRootPath);
        await Workbench.RememberPathAsync(RecentPathKind.BaselineFile, _sampleDataset.BaselinePath);
        await Workbench.RememberPathAsync(RecentPathKind.OutputRoot, _sampleDataset.OutputRootPath);

        await RefreshRecentPathsAsync();
        await RefreshBaselinePreviewAsync();
        await RefreshHistoryAsync();
        SetInfoNotice("Sample dataset paths were loaded into the execution form.");
    }

    private async Task HandleLoadSampleHotkeyAsync()
    {
        if (_isRunning)
        {
            SetInfoNotice("Sample dataset paths cannot be loaded while a compare run is in progress.");
            return;
        }

        if (!HasSampleDataset)
        {
            SetInfoNotice("Sample dataset is not available in this workspace.");
            return;
        }

        await LoadSampleDatasetAsync();
    }

    private async Task FocusSearchAsync()
    {
        _pendingSearchFocus = true;

        if (_isCompactInspectorOpen)
        {
            ShowCompactQueue();
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task MoveHistorySelectionAsync(int direction)
    {
        if (_history.Count == 0)
        {
            SetInfoNotice("Execution history is empty.");
            return;
        }

        var currentIndex = SelectedHistoryExecutionId is null
            ? -1
            : _history
                .Select((entry, index) => new { entry.ExecutionId, index })
                .FirstOrDefault(entry => string.Equals(entry.ExecutionId, SelectedHistoryExecutionId, StringComparison.OrdinalIgnoreCase))
                ?.index ?? -1;

        var nextIndex = direction > 0
            ? Math.Min(currentIndex + 1, _history.Count - 1)
            : currentIndex < 0
                ? _history.Count - 1
                : Math.Max(currentIndex - 1, 0);

        if (currentIndex < 0 && direction > 0)
        {
            nextIndex = 0;
        }

        SelectedHistoryExecutionId = _history[nextIndex].ExecutionId;

        await InvokeAsync(StateHasChanged);

        try
        {
            await JSRuntime.InvokeVoidAsync("guardianHotkeys.scrollSelectedHistory");
        }
        catch (JSException)
        {
        }
    }

    private async Task HandleLoadSelectedHistoryHotkeyAsync()
    {
        if (SelectedHistoryEntry is null)
        {
            SetInfoNotice("Select an execution history entry before loading it.");
            return;
        }

        await LoadHistoryAsync(SelectedHistoryEntry.JsonResultPath);
    }

    private void ShowShortcutOverlay()
        => _showShortcutOverlay = true;

    private void HideShortcutOverlay()
        => _showShortcutOverlay = false;

    private Task OpenSampleGuideAsync()
        => OpenFileAsync(_sampleDataset?.ReadmePath);

    private async Task OpenPathAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            ClearError();

            if (File.Exists(path))
            {
                await OpenFileAsync(path);
                return;
            }

            if (Directory.Exists(path))
            {
                await Launcher.Default.OpenAsync(new Uri(Path.GetFullPath(path)));
                return;
            }

            SetError($"The selected path could not be opened because it does not exist: {path}");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }

    private async Task OpenFileAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        await Launcher.Default.OpenAsync(new OpenFileRequest(Path.GetFileName(path), new ReadOnlyFile(path)));
    }

    private async Task BrowseBaselineAsync()
    {
        try
        {
            ClearError();
            var path = await PathSelector.PickBaselineFileAsync();
            await ApplyPathAsync(PathField.BaselinePath, path, rememberPath: true);
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }

    private async Task BrowseFolderAsync(PathField field)
    {
        try
        {
            ClearError();
            var path = await PathSelector.PickFolderAsync("Select");
            await ApplyPathAsync(field, path, rememberPath: true);
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }

    private Task ApplyRecentPathAsync(PathField field, string? path)
        => ApplyPathAsync(field, path, rememberPath: false);

    private async Task ApplyPathAsync(PathField field, string? path, bool rememberPath)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        switch (field)
        {
            case PathField.CurrentRoot:
                _runForm.CurrentRootPath = path;
                break;
            case PathField.PatchRoot:
                _runForm.PatchRootPath = path;
                break;
            case PathField.BaselinePath:
                _runForm.BaselinePath = path;
                await RefreshBaselinePreviewAsync();
                break;
            case PathField.OutputRoot:
                _runForm.OutputRootPath = path;
                await RefreshHistoryAsync();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(field), field, null);
        }

        if (rememberPath)
        {
            await Workbench.RememberPathAsync(MapRecentPathKind(field), path);
            await RefreshRecentPathsAsync();
        }
    }

    private Task OnBaselinePathChangedAsync()
        => RefreshBaselinePreviewAsync();

    private Task OnOutputRootChangedAsync()
        => RefreshHistoryAsync();

    private async Task LoadStoredSftpSecretsAsync()
    {
        _runForm.CurrentSftpPassword = await SecretStore.GetPasswordAsync(InputSide.Current) ?? string.Empty;
        _runForm.CurrentSftpPrivateKeyPassphrase = await SecretStore.GetPrivateKeyPassphraseAsync(InputSide.Current) ?? string.Empty;
        _runForm.PatchSftpPassword = await SecretStore.GetPasswordAsync(InputSide.Patch) ?? string.Empty;
        _runForm.PatchSftpPrivateKeyPassphrase = await SecretStore.GetPrivateKeyPassphraseAsync(InputSide.Patch) ?? string.Empty;
    }

    private Task PersistSftpSecretsAsync()
        => Task.WhenAll(
            SecretStore.SavePasswordAsync(InputSide.Current, _runForm.CurrentSftpPassword),
            SecretStore.SavePrivateKeyPassphraseAsync(InputSide.Current, _runForm.CurrentSftpPrivateKeyPassphrase),
            SecretStore.SavePasswordAsync(InputSide.Patch, _runForm.PatchSftpPassword),
            SecretStore.SavePrivateKeyPassphraseAsync(InputSide.Patch, _runForm.PatchSftpPrivateKeyPassphrase));

    private void SelectFilter(ResultFilter filter)
    {
        _selectedFilter = filter;
        SelectedRelativePath = FilteredItems.FirstOrDefault()?.RelativePath;
        if (SelectedRelativePath is null)
        {
            _isCompactInspectorOpen = false;
        }
    }

    private void SelectItem(string relativePath)
    {
        SelectedRelativePath = relativePath;
        _isCompactInspectorOpen = true;
    }

    private void ShowCompactQueue()
        => _isCompactInspectorOpen = false;

    private void ShowCompactInspector()
    {
        if (!CanToggleCompactInspector)
        {
            return;
        }

        _isCompactInspectorOpen = true;
    }

    private void HideCompactInspector()
        => ShowCompactQueue();

    private void HandleFilterHotkey(ResultFilter filter)
    {
        if (!HasReport)
        {
            SetInfoNotice("Filter shortcuts are available after the first execution.");
            return;
        }

        SelectFilter(filter);
    }

    private void HandleToggleSelectedRowHotkey()
    {
        if (SelectedItem is null)
        {
            SetInfoNotice("Select a comparison row before toggling bulk selection.");
            return;
        }

        var shouldSelect = !_bulkSelectedPaths.Contains(SelectedItem.RelativePath);
        SetBulkSelection(SelectedItem.RelativePath, shouldSelect);
        SetInfoNotice(shouldSelect
            ? $"Added {SelectedItem.RelativePath} to the bulk-selection set."
            : $"Removed {SelectedItem.RelativePath} from the bulk-selection set.");
    }

    private async Task MoveSelectionAsync(int direction)
    {
        if (FilteredItems.Count == 0)
        {
            return;
        }

        var currentIndex = SelectedRelativePath is null
            ? -1
            : FilteredItems
                .Select((item, index) => new { item.RelativePath, index })
                .FirstOrDefault(entry => string.Equals(entry.RelativePath, SelectedRelativePath, StringComparison.OrdinalIgnoreCase))
                ?.index ?? -1;

        var nextIndex = direction > 0
            ? Math.Min(currentIndex + 1, FilteredItems.Count - 1)
            : currentIndex < 0
                ? FilteredItems.Count - 1
                : Math.Max(currentIndex - 1, 0);

        if (currentIndex < 0 && direction > 0)
        {
            nextIndex = 0;
        }

        SelectedRelativePath = FilteredItems[nextIndex].RelativePath;

        await InvokeAsync(StateHasChanged);

        try
        {
            await JSRuntime.InvokeVoidAsync("guardianHotkeys.scrollSelectedRow");
        }
        catch (JSException)
        {
        }
    }

    private async Task HandleOpenSelectedArtifactHotkeyAsync(bool isCurrent)
    {
        if (SelectedItem is null)
        {
            SetInfoNotice("Select a comparison row before opening a source file.");
            return;
        }

        var canOpen = isCurrent ? CanOpenSelectedCurrentArtifact : CanOpenSelectedPatchArtifact;
        var path = isCurrent ? SelectedCurrentArtifactPath : SelectedPatchArtifactPath;

        if (!canOpen || string.IsNullOrWhiteSpace(path))
        {
            SetInfoNotice(isCurrent
                ? "The current-side file is not available for the selected result."
                : "The patch-side file is not available for the selected result.");
            return;
        }

        await OpenPathAsync(path);
        if (string.IsNullOrWhiteSpace(_errorMessage))
        {
            SetInfoNotice($"Opened {(isCurrent ? "current" : "patch")} file for {SelectedItem.RelativePath}.");
        }
    }

    private async Task HandleOpenReportArtifactHotkeyAsync(ReportArtifactShortcut artifact)
    {
        if (!HasReport)
        {
            SetInfoNotice("Run an inspection before opening report artifacts.");
            return;
        }

        string label;
        string? filePath;
        string? directoryPath;
        bool canOpen;

        switch (artifact)
        {
            case ReportArtifactShortcut.Html:
                label = "HTML report";
                filePath = _report!.Artifacts.HtmlReportPath;
                directoryPath = null;
                canOpen = CanOpenReportHtml;
                break;
            case ReportArtifactShortcut.Excel:
                label = "Excel report";
                filePath = _report!.Artifacts.ExcelReportPath;
                directoryPath = null;
                canOpen = CanOpenReportExcel;
                break;
            case ReportArtifactShortcut.Json:
                label = "JSON results";
                filePath = _report!.Artifacts.JsonResultPath;
                directoryPath = null;
                canOpen = CanOpenReportJson;
                break;
            case ReportArtifactShortcut.Log:
                label = "execution log";
                filePath = _report!.Artifacts.LogPath;
                directoryPath = null;
                canOpen = CanOpenReportLog;
                break;
            case ReportArtifactShortcut.OutputDirectory:
                label = "output directory";
                filePath = null;
                directoryPath = _report!.Artifacts.OutputDirectory;
                canOpen = CanOpenReportOutputDirectory;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(artifact), artifact, null);
        }

        if (!canOpen)
        {
            SetInfoNotice($"The current run {label} is not available.");
            return;
        }

        if (filePath is not null)
        {
            await OpenFileAsync(filePath);
        }
        else if (directoryPath is not null)
        {
            await OpenPathAsync(directoryPath);
        }

        if (string.IsNullOrWhiteSpace(_errorMessage))
        {
            SetInfoNotice($"Opened current run {label}.");
        }
    }

    private bool IsBulkSelected(string relativePath)
        => _bulkSelectedPaths.Contains(relativePath);

    private void SetBulkSelection(string relativePath, bool isSelected)
    {
        if (isSelected)
        {
            _bulkSelectedPaths.Add(relativePath);
        }
        else
        {
            _bulkSelectedPaths.Remove(relativePath);
        }
    }

    private void SelectAllVisibleItems()
    {
        foreach (var item in FilteredItems)
        {
            _bulkSelectedPaths.Add(item.RelativePath);
        }
    }

    private void HandleSelectVisibleHotkey()
    {
        if (FilteredItems.Count == 0)
        {
            SetInfoNotice("No visible results are available to select.");
            return;
        }

        SelectAllVisibleItems();
        SetInfoNotice($"Added {FilteredItems.Count} visible row{(FilteredItems.Count == 1 ? string.Empty : "s")} to the bulk-selection set.");
    }

    private void SelectVisibleReviewItems()
    {
        foreach (var item in FilteredItems.Where(static item => item.Status != CompareStatus.Ok))
        {
            _bulkSelectedPaths.Add(item.RelativePath);
        }
    }

    private void HandleSelectReviewSetHotkey()
    {
        var reviewItems = FilteredItems.Count(static item => item.Status != CompareStatus.Ok);
        if (reviewItems == 0)
        {
            SetInfoNotice("No review-set rows are available in the current filter.");
            return;
        }

        SelectVisibleReviewItems();
        SetInfoNotice($"Added {reviewItems} review-set row{(reviewItems == 1 ? string.Empty : "s")} to the bulk-selection set.");
    }

    private void ClearBulkSelection()
        => _bulkSelectedPaths.Clear();

    private void HandleClearBulkSelectionHotkey()
    {
        if (_bulkSelectedPaths.Count == 0)
        {
            SetInfoNotice("Bulk selection is already empty.");
            return;
        }

        var clearedCount = _bulkSelectedPaths.Count;
        ClearBulkSelection();
        SetInfoNotice($"Cleared {clearedCount} bulk-selected row{(clearedCount == 1 ? string.Empty : "s")}.");
    }

    private async Task OpenSelectedArtifactsAsync(bool isCurrent)
    {
        var paths = ResolveBulkArtifactPaths(isCurrent);
        if (paths.Count == 0)
        {
            SetError(isCurrent
                ? "No current-side files are available for the selected rows."
                : "No patch-side files are available for the selected rows.");
            return;
        }

        ClearError();

        foreach (var path in paths)
        {
            await OpenPathAsync(path);
            if (!string.IsNullOrWhiteSpace(_errorMessage))
            {
                return;
            }
        }

        SetInfoNotice($"Opened {paths.Count} {(isCurrent ? "current" : "patch")} file{(paths.Count == 1 ? string.Empty : "s")} from the current selection.");
    }

    private async Task HandleOpenBulkArtifactsHotkeyAsync(bool isCurrent)
    {
        if (_bulkSelectedPaths.Count == 0)
        {
            SetInfoNotice("Bulk open is available after selecting one or more rows.");
            return;
        }

        await OpenSelectedArtifactsAsync(isCurrent);
    }

    private void ApplyReport(ExecutionReport report)
    {
        _report = report;
        _bulkSelectedPaths.Clear();
        _isCompactInspectorOpen = false;
        SelectedHistoryExecutionId = report.Summary.ExecutionId;
        SelectedRelativePath = report.Result.Items
            .OrderByDescending(GetSeverityWeight)
            .ThenBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()?.RelativePath;
    }

    private void SyncSelectedHistoryEntry()
    {
        if (_history.Count == 0)
        {
            SelectedHistoryExecutionId = null;
            return;
        }

        if (!string.IsNullOrWhiteSpace(SelectedHistoryExecutionId)
            && _history.Any(entry => string.Equals(entry.ExecutionId, SelectedHistoryExecutionId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_report?.Summary.ExecutionId)
            && _history.Any(entry => string.Equals(entry.ExecutionId, _report.Summary.ExecutionId, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedHistoryExecutionId = _report.Summary.ExecutionId;
            return;
        }

        SelectedHistoryExecutionId = _history[0].ExecutionId;
    }

    private bool MatchesFilter(ComparisonItemResult item)
        => _selectedFilter switch
        {
            ResultFilter.All => true,
            ResultFilter.Changed => item.Status == CompareStatus.Changed,
            ResultFilter.MissingRequired => item.Status == CompareStatus.MissingRequired,
            ResultFilter.Error => item.Status == CompareStatus.Error,
            ResultFilter.Ok => item.Status == CompareStatus.Ok,
            _ => true,
        };

    private bool MatchesSearch(ComparisonItemResult item)
    {
        if (string.IsNullOrWhiteSpace(_searchText))
        {
            return true;
        }

        var probe = string.Join(' ', item.RelativePath, item.RuleId, item.Status, item.Severity, item.FileType, item.Summary);
        return probe.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    private string GetCount(string key)
        => !HasReport
            ? "0"
            : key == "Total"
                ? _report!.Summary.TotalFileCount.ToString()
                : _report!.Summary.StatusCounts.GetValueOrDefault(key, 0).ToString();

    private string GetCriticalCount()
    {
        if (!HasReport)
        {
            return "0";
        }

        var missingRequired = _report!.Summary.StatusCounts.GetValueOrDefault(CompareStatus.MissingRequired.ToString(), 0);
        var errors = _report.Summary.StatusCounts.GetValueOrDefault(CompareStatus.Error.ToString(), 0);
        return (missingRequired + errors).ToString();
    }

    private string GetFastestStageLabel()
    {
        var stage = PerformanceStages
            .Where(stage => stage.ItemCount > 0 && stage.ItemsPerSecond > 0)
            .OrderByDescending(stage => stage.ItemsPerSecond)
            .FirstOrDefault();

        return stage is null
            ? "-"
            : $"{stage.StageName} ({FormatThroughput(stage.ItemsPerSecond)})";
    }

    private static int GetSeverityWeight(ComparisonItemResult item)
        => item.Severity switch
        {
            Severity.Critical => 4,
            Severity.High => 3,
            Severity.Medium => 2,
            _ => 1,
        };

    private static string GetStatusBadgeClass(CompareStatus status)
        => GuardianBadgeStyles.ForStatus(status);

    private static string GetSeverityBadgeClass(Severity severity)
        => GuardianBadgeStyles.ForSeverity(severity);

    private static string FilterLabel(ResultFilter filter)
        => filter switch
        {
            ResultFilter.MissingRequired => "Missing Required",
            _ => filter.ToString(),
        };

    private static string FormatDuration(double durationMs)
        => durationMs >= 1000d
            ? $"{durationMs / 1000d:0.00} s"
            : $"{durationMs:0.##} ms";

    private static string FormatThroughput(double itemsPerSecond)
        => itemsPerSecond <= 0
            ? "-"
            : $"{itemsPerSecond:0.##} items/s";

    private void SetInfoNotice(string message)
    {
        CancelNoticeAutoDismiss();
        _errorMessage = null;
        _noticeKind = UiNoticeKind.Info;
        _noticeMessage = message;
        ScheduleNoticeAutoDismiss();
    }

    private void SetSuccessNotice(string message)
    {
        CancelNoticeAutoDismiss();
        _errorMessage = null;
        _noticeKind = UiNoticeKind.Success;
        _noticeMessage = message;
        ScheduleNoticeAutoDismiss();
    }

    private void SetError(string message)
    {
        CancelNoticeAutoDismiss();
        _noticeMessage = null;
        _errorMessage = message;
    }

    private void ClearError()
        => _errorMessage = null;

    private void ClearNotice()
    {
        CancelNoticeAutoDismiss();
        _noticeMessage = null;
    }

    private void ClearMessages()
    {
        CancelNoticeAutoDismiss();
        _errorMessage = null;
        _noticeMessage = null;
    }

    private void ScheduleNoticeAutoDismiss()
    {
        _noticeAutoDismissCts = new CancellationTokenSource();
        _ = AutoDismissNoticeAsync(_noticeAutoDismissCts.Token);
    }

    private async Task AutoDismissNoticeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(NoticeAutoDismissDelay, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await InvokeAsync(() =>
            {
                _noticeMessage = null;
                StateHasChanged();
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CancelNoticeAutoDismiss()
    {
        _noticeAutoDismissCts?.Cancel();
        _noticeAutoDismissCts?.Dispose();
        _noticeAutoDismissCts = null;
    }

    public void Dispose()
        => CancelNoticeAutoDismiss();

    public async ValueTask DisposeAsync()
    {
        CancelNoticeAutoDismiss();

        if (_hotkeysRegistered)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("guardianHotkeys.unregister");
            }
            catch (JSException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        _hotkeyReference?.Dispose();
        _hotkeyReference = null;
        _hotkeysRegistered = false;
    }

    private static SampleDatasetInfo? TryLocateSampleDataset()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var sampleRoot = Path.Combine(directory.FullName, "sample", "guardian");
            var currentRoot = Path.Combine(sampleRoot, "current");
            var patchRoot = Path.Combine(sampleRoot, "patch");
            var baselinePath = Path.Combine(sampleRoot, "baseline.xlsx");
            var outputRoot = Path.Combine(sampleRoot, "output");
            var readmePath = Path.Combine(sampleRoot, "README.md");

            if (Directory.Exists(currentRoot)
                && Directory.Exists(patchRoot)
                && Directory.Exists(outputRoot)
                && File.Exists(baselinePath)
                && File.Exists(readmePath))
            {
                return new SampleDatasetInfo(sampleRoot, currentRoot, patchRoot, baselinePath, outputRoot, readmePath);
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static bool CanOpenFile(string? path)
        => !string.IsNullOrWhiteSpace(path) && File.Exists(path);

    private static bool CanOpenDirectory(string? path)
        => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

    private InputSourceRequest BuildCurrentInputRequest()
        => BuildInputSourceRequest(
            InputSide.Current,
            _runForm.CurrentMode,
            _runForm.CurrentRootPath,
            _runForm.CurrentSftpHost,
            _runForm.CurrentSftpPort,
            _runForm.CurrentSftpUsername,
            _runForm.CurrentSftpAuthenticationMode,
            _runForm.CurrentSftpPassword,
            _runForm.CurrentSftpPrivateKeyPath,
            _runForm.CurrentSftpPrivateKeyPassphrase,
            _runForm.CurrentSftpRemoteRoot,
            _runForm.CurrentSftpFingerprint,
            _runForm.CurrentSftpClearTargetBeforeDownload);

    private InputSourceRequest BuildPatchInputRequest()
    {
        var host = _runForm.PatchUseCurrentSftpConnection ? _runForm.CurrentSftpHost : _runForm.PatchSftpHost;
        var port = _runForm.PatchUseCurrentSftpConnection ? _runForm.CurrentSftpPort : _runForm.PatchSftpPort;
        var username = _runForm.PatchUseCurrentSftpConnection ? _runForm.CurrentSftpUsername : _runForm.PatchSftpUsername;
        var authenticationMode = _runForm.PatchUseCurrentSftpConnection ? _runForm.CurrentSftpAuthenticationMode : _runForm.PatchSftpAuthenticationMode;
        var password = _runForm.PatchUseCurrentSftpConnection ? _runForm.CurrentSftpPassword : _runForm.PatchSftpPassword;
        var privateKeyPath = _runForm.PatchUseCurrentSftpConnection ? _runForm.CurrentSftpPrivateKeyPath : _runForm.PatchSftpPrivateKeyPath;
        var privateKeyPassphrase = _runForm.PatchUseCurrentSftpConnection ? _runForm.CurrentSftpPrivateKeyPassphrase : _runForm.PatchSftpPrivateKeyPassphrase;
        var fingerprint = _runForm.PatchUseCurrentSftpConnection ? _runForm.CurrentSftpFingerprint : _runForm.PatchSftpFingerprint;

        return BuildInputSourceRequest(
            InputSide.Patch,
            _runForm.PatchMode,
            _runForm.PatchRootPath,
            host,
            port,
            username,
            authenticationMode,
            password,
            privateKeyPath,
            privateKeyPassphrase,
            _runForm.PatchSftpRemoteRoot,
            fingerprint,
            _runForm.PatchSftpClearTargetBeforeDownload);
    }

    private static InputSourceRequest BuildInputSourceRequest(
        InputSide side,
        InputSourceMode mode,
        string localRootPath,
        string? host,
        int port,
        string? username,
        SftpAuthenticationMode authenticationMode,
        string? password,
        string? privateKeyPath,
        string? privateKeyPassphrase,
        string? remoteRootPath,
        string? fingerprint,
        bool clearTargetBeforeDownload)
    {
        if (mode == InputSourceMode.Local)
        {
            return new InputSourceRequest(side, mode, localRootPath);
        }

        return new InputSourceRequest(
            side,
            mode,
            localRootPath,
            new SftpInputRequest(
                host ?? string.Empty,
                port,
                username ?? string.Empty,
                remoteRootPath ?? string.Empty,
                authenticationMode == SftpAuthenticationMode.Password ? password ?? string.Empty : null,
                fingerprint,
                clearTargetBeforeDownload,
                authenticationMode,
                authenticationMode == SftpAuthenticationMode.PrivateKey ? privateKeyPath ?? string.Empty : null,
                authenticationMode == SftpAuthenticationMode.PrivateKey ? privateKeyPassphrase : null));
    }

    private static string? ResolveSelectedArtifactPath(string? rootPath, ComparisonItemResult? item, bool isCurrent)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || item is null)
        {
            return null;
        }

        if (item.RelativePath.IndexOfAny(['*', '?']) >= 0)
        {
            return null;
        }

        if (isCurrent && !item.CurrentExists)
        {
            return null;
        }

        if (!isCurrent && !item.PatchExists)
        {
            return null;
        }

        var relativePath = item.RelativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(rootPath, relativePath));
    }

    private IReadOnlyList<string> ResolveBulkArtifactPaths(bool isCurrent)
    {
        var rootPath = isCurrent ? _report?.Request.CurrentRootPath : _report?.Request.PatchRootPath;

        return BulkSelectedItems
            .Select(item => ResolveSelectedArtifactPath(rootPath, item, isCurrent))
            .Where(CanOpenFile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
    }

    private static PathStatusBadge EvaluateInputRootBadge(string? path, InputSourceMode mode)
        => mode == InputSourceMode.Local
            ? EvaluateDirectoryBadge(path)
            : EvaluateOutputDirectoryBadge(path);

    private static PathStatusBadge EvaluateDirectoryBadge(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new("Enter path", "bg-slate-100 text-slate-700 ring-1 ring-inset ring-slate-200", false);
        }

        if (Directory.Exists(path))
        {
            return new("Ready", "bg-emerald-100 text-emerald-700 ring-1 ring-inset ring-emerald-200", true);
        }

        if (File.Exists(path))
        {
            return new("Not folder", "bg-amber-100 text-amber-700 ring-1 ring-inset ring-amber-200", false);
        }

        return new("Missing", "bg-red-100 text-red-700 ring-1 ring-inset ring-red-200", false);
    }

    private static PathStatusBadge EvaluateOutputDirectoryBadge(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new("Enter path", "bg-slate-100 text-slate-700 ring-1 ring-inset ring-slate-200", false);
        }

        if (Directory.Exists(path))
        {
            return new("Ready", "bg-emerald-100 text-emerald-700 ring-1 ring-inset ring-emerald-200", true);
        }

        if (File.Exists(path))
        {
            return new("Not folder", "bg-amber-100 text-amber-700 ring-1 ring-inset ring-amber-200", false);
        }

        var parentPath = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path));
        if (!string.IsNullOrWhiteSpace(parentPath) && Directory.Exists(parentPath))
        {
            return new("Will create", "bg-blue-100 text-blue-700 ring-1 ring-inset ring-blue-200", true);
        }

        return new("Missing parent", "bg-red-100 text-red-700 ring-1 ring-inset ring-red-200", false);
    }

    private static PathStatusBadge EvaluateBaselineBadge(string? path, string? previewError)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new("Enter file", "bg-slate-100 text-slate-700 ring-1 ring-inset ring-slate-200", false);
        }

        if (!File.Exists(path))
        {
            return new("Missing", "bg-red-100 text-red-700 ring-1 ring-inset ring-red-200", false);
        }

        var extension = Path.GetExtension(path);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".xlsm", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase))
        {
            return new("Invalid type", "bg-amber-100 text-amber-700 ring-1 ring-inset ring-amber-200", false);
        }

        if (!string.IsNullOrWhiteSpace(previewError))
        {
            return new("Invalid", "bg-red-100 text-red-700 ring-1 ring-inset ring-red-200", false);
        }

        return new("Ready", "bg-emerald-100 text-emerald-700 ring-1 ring-inset ring-emerald-200", true);
    }


    private static RunReadinessStatus EvaluateRunReadiness(
        string? reportTitle,
        PathStatusBadge currentRoot,
        PathStatusBadge patchRoot,
        bool currentInputConfigReady,
        bool patchInputConfigReady,
        PathStatusBadge baseline,
        PathStatusBadge outputRoot,
        BaselinePreviewSummary? baselinePreview)
    {
        var blockers = new List<string>();

        if (string.IsNullOrWhiteSpace(reportTitle))
        {
            blockers.Add("report title");
        }

        if (!currentRoot.CanProceed)
        {
            blockers.Add("current root");
        }

        if (!currentInputConfigReady)
        {
            blockers.Add("current SFTP settings");
        }

        if (!patchRoot.CanProceed)
        {
            blockers.Add("patch root");
        }

        if (!patchInputConfigReady)
        {
            blockers.Add("patch SFTP settings");
        }

        if (!baseline.CanProceed || baselinePreview is null)
        {
            blockers.Add("baseline workbook");
        }

        if (!outputRoot.CanProceed)
        {
            blockers.Add("output root");
        }

        if (blockers.Count == 0)
        {
            return new(
                true,
                "Ready",
                "bg-emerald-100 text-emerald-700 ring-1 ring-inset ring-emerald-200",
                "All required inputs passed validation. Guardian can start the compare run.");
        }

        return new(
            false,
            "Action needed",
            "bg-amber-100 text-amber-700 ring-1 ring-inset ring-amber-200",
            $"Fill or fix {string.Join(", ", blockers)} before starting the compare run.");
    }


    private static bool EvaluateSftpConfigReady(
        InputSourceMode mode,
        string? host,
        string? username,
        SftpAuthenticationMode authenticationMode,
        string? password,
        string? privateKeyPath,
        string? remoteRoot)
    {
        if (mode == InputSourceMode.Local)
        {
            return true;
        }

        var hasCredentials = authenticationMode switch
        {
            SftpAuthenticationMode.Password => !string.IsNullOrWhiteSpace(password),
            SftpAuthenticationMode.PrivateKey => !string.IsNullOrWhiteSpace(privateKeyPath),
            _ => false,
        };

        return !string.IsNullOrWhiteSpace(host)
            && !string.IsNullOrWhiteSpace(username)
            && hasCredentials
            && !string.IsNullOrWhiteSpace(remoteRoot);
    }

    private static bool EvaluatePatchSftpConfigReady(RunFormModel model)
    {
        if (model.PatchMode == InputSourceMode.Local)
        {
            return true;
        }

        if (model.PatchUseCurrentSftpConnection)
        {
            return model.CurrentMode == InputSourceMode.Sftp
                   && EvaluateSftpConfigReady(
                       InputSourceMode.Sftp,
                       model.CurrentSftpHost,
                       model.CurrentSftpUsername,
                       model.CurrentSftpAuthenticationMode,
                       model.CurrentSftpPassword,
                       model.CurrentSftpPrivateKeyPath,
                       model.PatchSftpRemoteRoot);
        }

        return EvaluateSftpConfigReady(
            InputSourceMode.Sftp,
            model.PatchSftpHost,
            model.PatchSftpUsername,
            model.PatchSftpAuthenticationMode,
            model.PatchSftpPassword,
            model.PatchSftpPrivateKeyPath,
            model.PatchSftpRemoteRoot);
    }

    private void ClearSftpFields()
    {
        _runForm.CurrentSftpHost = string.Empty;
        _runForm.CurrentSftpPort = 22;
        _runForm.CurrentSftpUsername = string.Empty;
        _runForm.CurrentSftpAuthenticationMode = SftpAuthenticationMode.Password;
        _runForm.CurrentSftpPassword = string.Empty;
        _runForm.CurrentSftpPrivateKeyPath = string.Empty;
        _runForm.CurrentSftpPrivateKeyPassphrase = string.Empty;
        _runForm.CurrentSftpRemoteRoot = string.Empty;
        _runForm.CurrentSftpFingerprint = string.Empty;
        _runForm.CurrentSftpClearTargetBeforeDownload = false;
        _runForm.PatchSftpHost = string.Empty;
        _runForm.PatchSftpPort = 22;
        _runForm.PatchSftpUsername = string.Empty;
        _runForm.PatchSftpAuthenticationMode = SftpAuthenticationMode.Password;
        _runForm.PatchSftpPassword = string.Empty;
        _runForm.PatchSftpPrivateKeyPath = string.Empty;
        _runForm.PatchSftpPrivateKeyPassphrase = string.Empty;
        _runForm.PatchSftpRemoteRoot = string.Empty;
        _runForm.PatchSftpFingerprint = string.Empty;
        _runForm.PatchSftpClearTargetBeforeDownload = false;
    }


    private static RecentPathKind MapRecentPathKind(PathField field)
        => field switch
        {
            PathField.CurrentRoot => RecentPathKind.CurrentRoot,
            PathField.PatchRoot => RecentPathKind.PatchRoot,
            PathField.BaselinePath => RecentPathKind.BaselineFile,
            PathField.OutputRoot => RecentPathKind.OutputRoot,
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
        };
}
