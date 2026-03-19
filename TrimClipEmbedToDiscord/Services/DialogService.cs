using System.Windows;
using BamboozClipStudio.Views;

namespace BamboozClipStudio.Services;

public enum DialogButtons
{
    Ok,
    YesNo
}

public static class DialogService
{
    public static bool ShowInfo(string title, string message)
        => Show(title, message, DialogButtons.Ok);

    public static bool ShowWarning(string title, string message)
        => Show(title, message, DialogButtons.Ok);

    public static bool ShowError(string title, string message)
        => Show(title, message, DialogButtons.Ok);

    public static bool Confirm(string title, string message)
        => Show(title, message, DialogButtons.YesNo);

    static bool Show(string title, string message, DialogButtons buttons)
    {
        var dialog = new ThemedDialog(title, message, buttons)
        {
            Owner = ResolveOwner()
        };

        return dialog.ShowDialog() == true;
    }

    static Window? ResolveOwner()
    {
        if (Application.Current?.Windows == null)
            return null;

        var active = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        return active ?? Application.Current.MainWindow;
    }
}
