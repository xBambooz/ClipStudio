using BamboozClipStudio.Core;
using BamboozClipStudio.Models;
using BamboozClipStudio.Services;
using System.Windows.Media.Imaging;

namespace BamboozClipStudio.ViewModels;

public class FiltersViewModel : ObservableObject
{
    readonly FilterService _filterService;
    bool _isVisible;
    BitmapImage? _previewFrame;

    public FilterPreset Preset { get; } = new();

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public BitmapImage? PreviewFrame
    {
        get => _previewFrame;
        set => SetProperty(ref _previewFrame, value);
    }

    public RelayCommand ResetCommand { get; }
    public RelayCommand CloseCommand { get; }

    public event Action? FiltersChanged;

    public FiltersViewModel(FilterService filterService)
    {
        _filterService = filterService;
        ResetCommand = new RelayCommand(() => { Preset.Reset(); FiltersChanged?.Invoke(); });
        CloseCommand = new RelayCommand(() => IsVisible = false);

        Preset.PropertyChanged += (_, _) => FiltersChanged?.Invoke();
    }

    public string BuildVfChain() => _filterService.BuildVfChain(Preset);
    public bool HasFilters => !Preset.IsDefault;

    public void Toggle() => IsVisible = !IsVisible;
}
