using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using SahelBundleKeyboard.App.Infrastructure;
using SahelBundleKeyboard.App.ViewModels;
using SahelBundleKeyboard.Core.Automation;
using SahelBundleKeyboard.Core.Sequencing;
using SahelBundleKeyboard.Windows.Windows;

namespace SahelBundleKeyboard.App.Views;

/// <summary>
/// Compact always-on-top controller whose mouse clicks never activate the window
/// (WS_EX_NOACTIVATE + WM_MOUSEACTIVATE=MA_NOACTIVATE), so keyboard focus stays with
/// whatever application the operator is typing into — including Sahel.
/// Shows bundle selection, bundle count (+/-), countdown, progress and run controls.
/// </summary>
public sealed partial class FloatingControllerWindow : Window
{
    private readonly MainViewModel _viewModel;

    public FloatingControllerWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        InitializeComponent();

        AllowsTransparency = true;

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            FloatingWindowBehavior.ApplyNoActivateStyle(hwnd);
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        };

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.ProgressText))
            {
                ProgressLabel.Text = viewModel.ProgressText;
                ControllerProgress.Value = viewModel.ProgressFraction;
            }
            else if (e.PropertyName is nameof(MainViewModel.StateText))
            {
                GoButton.IsEnabled = !viewModel.IsBusy;
                if (viewModel.State != AutomationState.Countdown)
                {
                    CountdownText.Text = string.Empty;
                }
            }
            else if (e.PropertyName is nameof(MainViewModel.StatusMessage))
            {
                if (viewModel.State == AutomationState.Countdown && viewModel.StatusMessage.Length > 0)
                {
                    var digits = new string(viewModel.StatusMessage.Where(char.IsDigit).ToArray());
                    CountdownText.Text = digits.Length > 0 ? digits : CountdownText.Text;
                }
                else if (viewModel.State is AutomationState.Running or AutomationState.Idle)
                {
                    CountdownText.Text = string.Empty;
                }
            }
        };
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var result = FloatingWindowBehavior.HandleMessage((uint)msg);
        if (result is not null)
        {
            handled = true;
            return result.Value;
        }

        return IntPtr.Zero;
    }

    private void OnDragMoveRequested(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // mouse already released; ignore
            }
        }
    }

    private void OnHideClick(object sender, RoutedEventArgs e) => _viewModel.SetControllerVisible(false);

    private void OnPlusClick(object sender, RoutedEventArgs e) => AdjustCount(+1);

    private void OnMinusClick(object sender, RoutedEventArgs e) => AdjustCount(-1);

    private void AdjustCount(int delta)
    {
        var current = QuantityFormatter.TryParse(_viewModel.BundleCountText, out var value) ? (int)value : 1;
        var next = Math.Clamp(current + delta, 1, 9999);
        _viewModel.BundleCountText = next.ToString();
    }

    private void OnGoClick(object sender, RoutedEventArgs e) => _viewModel.Start();

    private void OnStopClick(object sender, RoutedEventArgs e) => _viewModel.Stop();
}
