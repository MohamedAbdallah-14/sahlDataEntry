using System.Windows;
using SahelBundleKeyboard.App.Infrastructure;
using SahelBundleKeyboard.App.Services;
using SahelBundleKeyboard.App.ViewModels;
using SahelBundleKeyboard.App.Views;
using SahelBundleKeyboard.Core.Automation;
using SahelBundleKeyboard.Core.Hotkeys;
using SahelBundleKeyboard.Core.Logging;
using SahelBundleKeyboard.Infrastructure.Backup;
using SahelBundleKeyboard.Infrastructure.Logging;
using SahelBundleKeyboard.Infrastructure.Persistence;
using SahelBundleKeyboard.Windows.Hotkeys;
using SahelBundleKeyboard.Windows.Input;

namespace SahelBundleKeyboard.App;

public partial class App : Application
{
    private static readonly string LogSource = "App";

    private RollingFileLogger? _logger;
    private GlobalHotkeyManager? _hotkeys;
    private AutomationEngine? _engine;
    private MainViewModel? _mainViewModel;
    private FloatingControllerWindow? _controller;
    private AppDataService? _dataService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            _logger?.Error(LogSource, "Unhandled UI exception.", args.Exception);
            UiText.ShowError("حدث خطأ غير متوقع. راجع ملف السجل داخل مجلد Data/logs.");
            args.Handled = true;
        };

        // Composition root: manual dependency wiring, no framework.
        var paths = DataPaths.ResolveForCurrentProcess();
        _logger = new RollingFileLogger(paths.LogsFolder);
        _logger.Info(LogSource, $"Starting. Version={typeof(App).Assembly.GetName().Version}, BaseDir={paths.Root}");

        _dataService = new AppDataService(new JsonDataStore(paths, _logger), _logger);
        _dataService.LoadOrCreate();

        _engine = new AutomationEngine(
            new SendInputKeystrokeSender(),
            new TaskDelayService(),
            _logger);

        _hotkeys = new GlobalHotkeyManager();
        _hotkeys.HotkeyPressed += OnGlobalHotkeyPressed;

        _mainViewModel = new MainViewModel(
            _dataService,
            request => _engine!.TryStartAsync(request),
            () => _engine!.Pause(),
            () => _engine!.Resume(),
            () => _engine!.Stop(),
            status => { /* controller updates via property bindings */ });

        _engine.StateChanged += (_, args) => SafeDispatch(() => _mainViewModel.OnEngineStateChanged(args));
        _engine.ProgressChanged += (_, args) => SafeDispatch(() => _mainViewModel.OnEngineProgress(args));

        _mainViewModel.BundlesVm = new BundlesViewModel(_mainViewModel, _dataService);
        _mainViewModel.Settings = new SettingsViewModel(
            _dataService,
            _hotkeys,
            new BackupService(paths, _logger),
            ReapplyShortcuts);

        MainWindow = new MainWindow { DataContext = _mainViewModel };

        // Controller visibility follows the toggle on the run tab.
        _mainViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.ControllerVisible))
            {
                if (_mainViewModel.ControllerVisible)
                {
                    ShowController();
                }
                else
                {
                    HideController();
                }
            }
        };

        ApplyShortcuts(reportErrors: true);

        if (_dataService.StartupWarning is not null)
        {
            UiText.ShowError(_dataService.StartupWarning);
        }

        MainWindow.Show();
    }

    private void OnGlobalHotkeyPressed(string actionId)
    {
        SafeDispatch(() => _mainViewModel?.OnHotkeyPressed(actionId));
    }

    private void SafeDispatch(Action action)
    {
        try
        {
            Dispatcher.Invoke(action);
        }
        catch (Exception ex)
        {
            _logger?.Error(LogSource, "Dispatcher callback failed.", ex);
        }
    }

    /// <summary>Applies the current shortcut set; reports conflicts in Arabic and keeps the last valid one.</summary>
    private void ApplyShortcuts(bool reportErrors)
    {
        if (_hotkeys is null || _dataService is null)
        {
            return;
        }

        var s = _dataService.Document.Settings;

        HotkeyCombo start = HotkeyParser.TryParse(s.StartShortcut) ?? HotkeyParser.TryParse("Ctrl+Alt+G")!;
        HotkeyCombo pause = HotkeyParser.TryParse(s.PauseResumeShortcut) ?? HotkeyParser.TryParse("Ctrl+Alt+P")!;
        HotkeyCombo stop = HotkeyParser.TryParse(s.StopShortcut) ?? HotkeyParser.TryParse("Ctrl+Alt+S")!;

        try
        {
            _hotkeys.Apply(
            [
                new HotkeyEntry(HotkeyIdsStart, "Start", start),
                new HotkeyEntry(HotkeyIdsPauseResume, "PauseResume", pause),
                new HotkeyEntry(HotkeyIdsStop, "Stop", stop)
            ]);
        }
        catch (HotkeyRegistrationException ex)
        {
            _logger!.Warn(LogSource, $"Hotkey apply failed: {ex.Message}");
            if (reportErrors)
            {
                UiText.ShowError(ex.UserMessage + "\nتم الإبقاء على الاختصارات السابقة الصالحة.");
            }
        }
    }

    private void ReapplyShortcuts() => ApplyShortcuts(reportErrors: true);

    public void ShowController()
    {
        if (_controller is null && _mainViewModel is not null)
        {
            _controller = new FloatingControllerWindow(_mainViewModel);
            _controller.Closed += (_, _) => _controller = null;
        }

        if (_controller is not null && !((Window)_controller).IsVisible)
        {
            // ShowActivated=false keeps focus with the foreground application.
            ((Window)_controller).Show();
        }
    }

    public void HideController()
    {
        _controller?.Hide();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _engine?.Stop();
        _hotkeys?.Dispose();
        _controller?.Close();
        _logger?.Info(LogSource, "Exited.");
        base.OnExit(e);
    }

    private const int HotkeyIdsStart = 1;
    private const int HotkeyIdsPauseResume = 2;
    private const int HotkeyIdsStop = 3;
}
