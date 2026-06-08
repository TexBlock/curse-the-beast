using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using CurseTheBeast.GUI.ViewModels;
using System.ComponentModel;

namespace CurseTheBeast.GUI.Views;

public partial class MainWindow : AppWindow
{
    MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        if (RootNav.MenuItems.Count > 0)
        {
            RootNav.SelectedItem = RootNav.MenuItems[0];
        }

        SetCurrentPage("modpacks");
    }

    void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel == null)
        {
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyThemeMode(_viewModel.ThemeMode);
        ApplyWindowTransparencyMode(_viewModel.WindowTransparencyMode);
    }

    void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.ThemeMode) && _viewModel != null)
        {
            ApplyThemeMode(_viewModel.ThemeMode);
        }

        if (e.PropertyName == nameof(MainWindowViewModel.WindowTransparencyMode) && _viewModel != null)
        {
            ApplyWindowTransparencyMode(_viewModel.WindowTransparencyMode);
        }
    }

    void ApplyThemeMode(string themeMode)
    {
        var variant = themeMode switch
        {
            "Dark" => ThemeVariant.Dark,
            "Light" => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };

        RequestedThemeVariant = variant;
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = variant;
        }
    }

    void ApplyWindowTransparencyMode(string mode)
    {
        TransparencyLevelHint = mode switch
        {
            "AcrylicBlur" => [WindowTransparencyLevel.AcrylicBlur],
            "Mica" => [WindowTransparencyLevel.Mica],
            _ => [WindowTransparencyLevel.AcrylicBlur]
        };
    }

    void OnNavigationSelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
    {
        if (e.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            SetCurrentPage(tag);
        }
    }

    void SetCurrentPage(string tag)
    {
        var showModpacks = string.Equals(tag, "modpacks", StringComparison.OrdinalIgnoreCase);
        var showSettings = string.Equals(tag, "settings", StringComparison.OrdinalIgnoreCase);
        var showLogs = string.Equals(tag, "logs", StringComparison.OrdinalIgnoreCase);
        var showAbout = string.Equals(tag, "about", StringComparison.OrdinalIgnoreCase);
        ModpacksPage.IsVisible = showModpacks;
        SettingsPage.IsVisible = showSettings;
        LogsPage.IsVisible = showLogs;
        AboutPage.IsVisible = showAbout;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }

        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnClosed(e);
    }
}
