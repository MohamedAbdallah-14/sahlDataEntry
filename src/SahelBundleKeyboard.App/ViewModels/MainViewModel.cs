using System.Collections.ObjectModel;
using System.Windows;
using SahelBundleKeyboard.App.Infrastructure;
using SahelBundleKeyboard.App.Services;
using SahelBundleKeyboard.Core.Automation;
using SahelBundleKeyboard.Core.Models;
using SahelBundleKeyboard.Core.Sequencing;
using SahelBundleKeyboard.Windows.Hotkeys;

namespace SahelBundleKeyboard.App.ViewModels;

/// <summary>
/// Orchestrates bundle selection, run control, engine state, and persistence.
/// All engine events are marshaled to the UI thread by the composition root before
/// reaching this class; commands here always execute on the dispatcher thread.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly AppDataService _data;
    private readonly Func<RunRequest, Task<StartRunResult>> _startEngine;
    private readonly Action _stopEngine;
    private readonly Action<string> _showStatus;

    private EditableBundle? _selectedBundle;
    private string _bundleCountText = "1";
    private string _stateText = UiText.StateName(AutomationState.Idle);
    private AutomationState _state = AutomationState.Idle;
    private string _statusMessage = string.Empty;
    private int _progressCurrent;
    private int _progressTotal;
    private double _progressFraction;
    private bool _controllerVisible;

    public MainViewModel(
        AppDataService data,
        Func<RunRequest, Task<StartRunResult>> startEngine,
        Action stopEngine,
        Action<string> showStatus)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _startEngine = startEngine ?? throw new ArgumentNullException(nameof(startEngine));
        _stopEngine = stopEngine;
        _showStatus = showStatus ?? throw new ArgumentNullException(nameof(showStatus));

        Bundles = [];
        foreach (var bundle in data.Document.Bundles.Select(EditableBundle.FromModel))
        {
            Bundles.Add(bundle);
        }

        SelectedBundle = Bundles.FirstOrDefault(b => b.Id.ToString() == data.Document.Settings.LastSelectedBundleId) ??
                         Bundles.FirstOrDefault();

        BundleCountText = Math.Max(1, data.Document.Settings.LastBundleCount).ToString();

        StartCommand = new RelayCommand(Start, () => State is not (AutomationState.Countdown or AutomationState.Running or AutomationState.Paused));
        StopCommand = new RelayCommand(Stop, () => State is AutomationState.Countdown or AutomationState.Running or AutomationState.Paused);
        ToggleControllerCommand = new RelayCommand(ToggleController);
    }

    public ObservableCollection<EditableBundle> Bundles { get; }

    public EditableBundle? SelectedBundle
    {
        get => _selectedBundle;
        set
        {
            if (SetProperty(ref _selectedBundle, value))
            {
                _data.Document.Settings.LastSelectedBundleId = value?.Id.ToString();
                OnPropertyChanged(nameof(ItemCountSummary));
                OnPropertyChanged(nameof(SummaryDetails));
                _data.Save();
            }
        }
    }

    public string BundleCountText
    {
        get => _bundleCountText;
        set
        {
            if (SetProperty(ref _bundleCountText, value))
            {
                if (int.TryParse(QuantityFormatter.NormalizeDigits(value.Trim()), out var count) && count >= 1)
                {
                    _data.Document.Settings.LastBundleCount = count;
                    _data.Save();
                }

                OnPropertyChanged(nameof(ItemCountSummary));
                OnPropertyChanged(nameof(SummaryDetails));
            }
        }
    }

    public string ItemCountSummary
    {
        get
        {
            var hasValidCount = TryGetValidatedCount(out var validCount, out _);
            var items = SelectedBundle?.Items.Count ?? 0;
            var noun = items switch { 0 => "أصناف", 1 => "صنف", 2 => "صنفان", <= 10 => "أصناف", _ => "صنفاً" };
            return $"عدد الأصناف: {items} {noun} — عدد الحزم: {(hasValidCount ? validCount.ToString() : "غير صالح")}";
        }
    }

    public string SummaryDetails
    {
        get
        {
            var bundle = SelectedBundle;
            if (bundle is null)
            {
                return "اختر حزمة من القائمة.";
            }

            if (!TryGetValidatedCount(out var count, out _))
            {
                return "أدخل عدد حزم صحيحاً (عدد صحيح موجب).";
            }

            var lines = bundle.Items.Take(6).Select(i =>
            {
                var search = i.ProductCode.Trim().Length > 0 ? i.ProductCode : i.ProductName;
                var finalQuantity = FormatQuantityForSummary(i);
                var priceNote = i.CustomPriceText.Trim().Length > 0 ? " — بسعر مخصص" : string.Empty;
                return $"{search} × {finalQuantity}{priceNote}";
            });

            var more = bundle.Items.Count > 6 ? $"\n… و {bundle.Items.Count - 6} أصناف أخرى" : string.Empty;
            var totalActions = bundle.Items.Count * count;
            return string.Join("\n", lines) + more + $"\nإجمالي مرات الإدخال المتوقعة: {totalActions}";
        }
    }

    private string FormatQuantityForSummary(EditableBundleItem item)
    {
        if (!QuantityFormatter.TryParse(item.BaseQuantityText, out var quantity))
        {
            return "?";
        }

        if (!QuantityFormatter.TryParse(BundleCountText, out var count))
        {
            return QuantityFormatter.Format(quantity);
        }

        return QuantityFormatter.Format(quantity * count);
    }

    public string StateText
    {
        get => _stateText;
        private set => SetProperty(ref _stateText, value);
    }

    public AutomationState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                StateText = UiText.StateName(value);
                ((RelayCommand)StartCommand).RaiseCanExecuteChanged();
                ((RelayCommand)StopCommand).RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(ControllerStateBadge));
            }
        }
    }

    public bool IsBusy => State is AutomationState.Countdown or AutomationState.Running or AutomationState.Paused;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public System.Windows.Media.Brush StateBadgeBrush => State switch
    {
        AutomationState.Idle => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x7A, 0x86, 0x99)),
        AutomationState.Countdown => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD3, 0x84, 0x1B)),
        AutomationState.Running => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1B, 0x7A, 0x43)),
        AutomationState.Paused => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC0, 0x6C, 0x0E)),
        AutomationState.Stopped => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8E, 0x44, 0x3D)),
        AutomationState.Completed => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x14, 0x7D, 0x73)),
        AutomationState.Error => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC0, 0x39, 0x2B)),
        _ => System.Windows.Media.Brushes.Gray
    };

    public string ToggleControllerLabel =>
        ControllerVisible ? "إخفاء وحدة التحكم العائمة" : "إظهار وحدة التحكم العائمة";

    public string StartWarning => UiText.StartWarning;

    public int ProgressCurrent
    {
        get => _progressCurrent;
        private set => SetProperty(ref _progressCurrent, value);
    }

    public int ProgressTotal
    {
        get => _progressTotal;
        private set => SetProperty(ref _progressTotal, value);
    }

    public double ProgressFraction
    {
        get => _progressFraction;
        private set => SetProperty(ref _progressFraction, value);
    }

    public string ProgressText => ProgressTotal > 0 ? $"{ProgressCurrent} / {ProgressTotal}" : "— / —";

    public bool ControllerVisible
    {
        get => _controllerVisible;
        private set => SetProperty(ref _controllerVisible, value);
    }

    /// <summary>Snapshot for the floating controller binding.</summary>
    public string ControllerTitle =>
        SelectedBundle is null ? "لا توجد حزمة" : $"{SelectedBundle.Name} — عدد: {BundleCountText}";

    public string ControllerStateBadge => StateText;

    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ToggleControllerCommand { get; }

    /// <summary>Sub view-model for the bundles tab (assigned by the composition root).</summary>
    public BundlesViewModel? BundlesVm { get; set; }

    /// <summary>Sub view-model for the settings tab (assigned by the composition root).</summary>
    public SettingsViewModel? Settings { get; set; }

    private bool TryGetValidatedCount(out int count, out string error)
    {
        error = string.Empty;
        count = 0;

        var normalized = QuantityFormatter.NormalizeDigits(BundleCountText.Trim());
        if (normalized.Length == 0 || !normalized.All(char.IsAsciiDigit))
        {
            error = "عدد الحزم يجب أن يكون عدداً صحيحاً موجباً.";
            return false;
        }

        if (!int.TryParse(normalized, out count) || count < SettingLimits.MinBundleCount)
        {
            error = "عدد الحزم يجب أن يكون عدداً صحيحاً موجباً.";
            return false;
        }

        return true;
    }

    public void ToggleController()
    {
        ControllerVisible = !ControllerVisible;
        OnPropertyChanged(nameof(ToggleControllerLabel));
    }

    public void SetControllerVisible(bool visible)
    {
        if (ControllerVisible == visible)
        {
            return;
        }

        ControllerVisible = visible;
        OnPropertyChanged(nameof(ToggleControllerLabel));
    }

    public async void Start()
    {
        if (_selectedBundle is null)
        {
            UiText.ShowError("اختر حزمة أولاً.");
            return;
        }

        if (!_selectedBundle.Items.Any())
        {
            UiText.ShowError("الحزمة فارغة. أضف أصنافاً من تبويب \"الحزم\".");
            return;
        }

        // Commit any pending grid edits and validate rows before starting.
        foreach (var item in _selectedBundle.Items)
        {
            if (!item.TryCommit(out var itemError))
            {
                UiText.ShowError(itemError);
                return;
            }

            if (item.HasValidationError)
            {
                UiText.ShowError($"الصف \"{item.DisplayName}\" يحتوي قيماً غير صالحة.");
                return;
            }
        }

        if (!TryGetValidatedCount(out var count, out var countError))
        {
            UiText.ShowError(countError);
            return;
        }

        _selectedBundle.ReapplyOrder();
        _data.Save();

        ProgressTotal = _selectedBundle.Items.Count * count;
        ProgressCurrent = 0;
        ProgressFraction = 0;
        StatusMessage = string.Empty;

        var request = new RunRequest(
            _selectedBundle.Model,
            count,
            _data.Document.Settings.CountdownSeconds,
            _data.Document.Settings.DelayMilliseconds);

        var result = await _startEngine(request);
        if (!result.Success)
        {
            UiText.ShowError(result.ErrorMessage!);
        }
    }

    public void Stop() => _stopEngine();

    // ---- Engine callbacks (invoked on the dispatcher thread by the composition root) ----

    public void OnEngineStateChanged(AutomationStateChangedEventArgs e)
    {
        State = e.Current;
        StatusMessage = e.Message ?? string.Empty;
        _showStatus(e.Message ?? string.Empty);

        OnPropertyChanged(nameof(ControllerTitle));
    }

    public void OnEngineProgress(ProgressEventArgs e)
    {
        ProgressCurrent = e.CurrentItem;
        ProgressTotal = e.TotalItems;
        ProgressFraction = e.TotalItems == 0 ? 0 : (double)e.CurrentItem / e.TotalItems;
        OnPropertyChanged(nameof(ProgressText));
    }

    public void OnHotkeyPressed(string actionId)
    {
        switch (actionId)
        {
            case "Start":
                Start();
                break;
            case "Stop":
                Stop();
                break;
        }
    }

    // ---- Collection management helpers used by the bundles tab VM layer ----

    public void AddBundle(Bundle bundle)
    {
        var editable = EditableBundle.FromModel(bundle);
        Bundles.Add(editable);
        _data.Document.Bundles.Add(bundle);
        SelectedBundle = editable;
        _data.Save();
    }

    /// <summary>Removes an explicit bundle instance; never dereferences current selection.</summary>
    public void RemoveBundle(EditableBundle? bundle)
    {
        if (bundle is null)
        {
            return;
        }

        Bundles.Remove(bundle);

        var modelMatch = _data.Document.Bundles.FirstOrDefault(b => b.Id == bundle.Id);
        if (modelMatch is not null)
        {
            _ = _data.Document.Bundles.Remove(modelMatch);
        }

        if (ReferenceEquals(_selectedBundle, bundle))
        {
            SelectedBundle = Bundles.FirstOrDefault();
        }

        _data.Save();
        OnPropertyChanged(nameof(ItemCountSummary));
        OnPropertyChanged(nameof(ControllerTitle));
    }

    public void RefreshSummaries()
    {
        OnPropertyChanged(nameof(ItemCountSummary));
        OnPropertyChanged(nameof(SummaryDetails));
        OnPropertyChanged(nameof(ControllerTitle));

        if (SelectedBundle is not null)
        {
            foreach (var item in SelectedBundle.Items)
            {
                item.RefreshFromModel();
            }
        }
    }
}
