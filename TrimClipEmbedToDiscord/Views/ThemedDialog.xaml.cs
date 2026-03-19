using System.Windows;
using System.Windows.Input;
using BamboozClipStudio.Services;

namespace BamboozClipStudio.Views;

public partial class ThemedDialog : Window
{
    public string DialogTitle { get; }
    public string Message { get; }
    public string PrimaryButtonText { get; }
    public string SecondaryButtonText { get; }
    public bool ShowSecondaryButton { get; }

    public ThemedDialog(string title, string message, DialogButtons buttons)
    {
        DialogTitle = title;
        Message = message;
        PrimaryButtonText = buttons == DialogButtons.YesNo ? "Yes" : "OK";
        SecondaryButtonText = "No";
        ShowSecondaryButton = buttons == DialogButtons.YesNo;

        InitializeComponent();
        DataContext = this;
    }

    void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    void Primary_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    void Secondary_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
