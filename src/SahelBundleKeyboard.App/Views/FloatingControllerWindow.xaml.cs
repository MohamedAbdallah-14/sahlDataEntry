using System.Windows;
using System.Windows.Interop;
using SahelBundleKeyboard.App.Infrastructure;
using SahelBundleKeyboard.App.ViewModels;
using SahelBundleKeyboard.Core.Automation;
using SahelBundleKeyboard.Windows.Windows;

namespace SahelBundleKeyboard.App.Views;

/// <summary>
/// Compact always-on-top controller whose mouse clicks never activate the window
/// (WS_EX_NOACTIVATE + WM_MOUSEACTIVATE=MA_NOACTIVATE), so keyboard focus stays with
/// whatever application the operator is typing into — including Sahel.
/// </summary>
public sealed partial class FloatingControllerWindow : Window
{
    private readonly MainViewModel _viewModel;

    public FloatingControllerWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
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
            if (e.PropertyName is nameof(MainViewModel.ControllerTitle))
            {
                TitleText.Text = viewModel.ControllerTitle;
            }
            else if (e.PropertyName is nameof(MainViewModel.ProgressText))
            {
                ProgressLabel.Text = viewModel.ProgressText;
                ControllerProgress.Value = viewModel.ProgressFraction;
            }
            else if (e.PropertyName is nameof(MainViewModel.StateText))
            {
                PauseButton.Content = viewModel.PauseResumeLabel;
                GoButton.IsEnabled = !viewModel.IsBusy;
                if (viewModel.State != AutomationState.Countdown)
                {
                    CountdownText.Text = string.Empty;
                }
            }
            else if (e.PropertyName is nameof(MainViewModel.StatusMessage))
            {
                // Show countdown seconds big; other statuses inline.
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

        TitleText.Text = viewModel.ControllerTitle;
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

    private void OnDragMoveRequested(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // DragMove works without activation; focus stays with the foreground app.
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
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

    private void OnGoClick(object sender, RoutedEventArgs e) => _viewModel.Start();

    private void OnPauseResumeClick(object sender, RoutedEventArgs e) => _viewModel.PauseResume();

    private void OnStopClick(object sender, RoutedEventArgs e) => _viewModel.Stop();
}
