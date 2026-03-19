using BamboozClipStudio.ViewModels;
using System.Windows;

namespace BamboozClipStudio.Views;

public partial class UploadProgressDialog : Window
{
    readonly UploadProgressViewModel _vm;

    public UploadProgressDialog(UploadProgressViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        vm.CloseRequested = Close;
    }

    void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.IsDone && !_vm.IsError)
            _vm.CancelCommand.Execute(null);

        Close();
    }
}
