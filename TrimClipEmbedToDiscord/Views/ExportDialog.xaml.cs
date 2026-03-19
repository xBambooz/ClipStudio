using BamboozClipStudio.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace BamboozClipStudio.Views;

public partial class ExportDialog : Window
{
    public ExportDialog(ExportViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.UpdateEstimatedSize();
    }

    void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
    void Close_Click(object sender, RoutedEventArgs e) => Close();
    void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
