using BamboozClipStudio.ViewModels;
using BamboozClipStudio.Views;
using System.Windows;
using System.Windows.Input;

namespace BamboozClipStudio;

public partial class MainWindow : Window
{
    readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        KeyDown += OnKeyDown;
        Loaded += async (_, _) => await vm.InitializeAsync();
    }

    // ── Window chrome ──────────────────────────────────────────────────────

    void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) ToggleMaximize();
        else DragMove();
    }

    void Minimize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    void ToggleMaximize()
        => WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal : WindowState.Maximized;

    void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ── Keyboard shortcuts ─────────────────────────────────────────────────

    void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Space:
                _vm.Timeline.TogglePlayback();
                e.Handled = true;
                break;
            case Key.O when Keyboard.Modifiers == ModifierKeys.Control:
                _vm.OpenMediaCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Left:
                _vm.Timeline.StepBackCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Right:
                _vm.Timeline.StepForwardCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    // ── Drag and drop ──────────────────────────────────────────────────────

    void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    async void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files.Length > 0)
            await _vm.LoadFileAsync(files[0]);
    }

    // ── Drop zone click ────────────────────────────────────────────────────

    void DropZone_Click(object sender, MouseButtonEventArgs e)
        => _vm.OpenMediaCommand.Execute(null);

    // ── Menus ──────────────────────────────────────────────────────────────

    void FileMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.DataContext = _vm;
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }

    void ToolsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.DataContext = _vm;
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }

    void OpenMedia_Click(object sender, RoutedEventArgs e)
        => _vm.OpenMediaCommand.Execute(null);

    void RegisterContextMenu_Click(object sender, RoutedEventArgs e)
        => _vm.RegisterContextMenuCommand.Execute(null);

    void AutoUpdate_Click(object sender, RoutedEventArgs e)
    {
        // IsChecked binding handles the toggle; just ensure save
    }

    // ── Export ─────────────────────────────────────────────────────────────

    void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ExportDialog(_vm.Export) { Owner = this };
        dialog.ShowDialog();
    }
}
