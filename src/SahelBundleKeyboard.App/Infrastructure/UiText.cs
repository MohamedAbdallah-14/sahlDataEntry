using System.Windows;

namespace SahelBundleKeyboard.App.Infrastructure;

/// <summary>Central Arabic user-facing strings and dialog helpers.</summary>
public static class UiText
{
    public const string AppTitle = "إدخال حزم سهل";

    // Tabs
    public const string TabRun = "التشغيل";
    public const string TabBundles = "الحزم";
    public const string TabSettings = "الإعدادات";

    // States
    public static string StateName(Core.Automation.AutomationState state) => state switch
    {
        Core.Automation.AutomationState.Idle => "جاهز",
        Core.Automation.AutomationState.Countdown => "عد تنازلي",
        Core.Automation.AutomationState.Running => "قيد الإدخال",
        Core.Automation.AutomationState.Paused => "موقوف مؤقتاً",
        Core.Automation.AutomationState.Stopped => "تم الإيقاف",
        Core.Automation.AutomationState.Completed => "اكتمل",
        Core.Automation.AutomationState.Error => "خطأ",
        _ => "غير معروف"
    };

    public const string StartWarning =
        "تنبيه: إذا كان نافذة هذا البرنامج هي النشطة عند الضغط على ابدأ، فسيتم الكتابة داخل هذا البرنامج نفسه. " +
        "اضغط أولاً في حقل البحث ببرنامج سهل ثم استخدم الاختصار العام أو زر وحدة التحكم العائمة.";

    public const string StopIrreversibleNote =
        "ملحوظة: الإيقاف لا يمكنه التراجع عن النص الذي تم إدخاله بالفعل قبل الإيقاف.";

    private const MessageBoxOptions Rtl = MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign;

    private static Window OwnerWindow =>
        Application.Current.MainWindow ?? Application.Current.Windows.OfType<Window>().FirstOrDefault()
        ?? new Window();

    public static void ShowError(string message) =>
        _ = MessageBox.Show(OwnerWindow, message, AppTitle,
            MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, Rtl);

    public static void ShowInfo(string message) =>
        _ = MessageBox.Show(OwnerWindow, message, AppTitle,
            MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, Rtl);

    public static bool Confirm(string message) =>
        MessageBox.Show(OwnerWindow, message, AppTitle,
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No, Rtl)
        == MessageBoxResult.Yes;
}
