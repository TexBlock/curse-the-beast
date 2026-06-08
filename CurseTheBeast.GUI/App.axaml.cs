using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using CurseTheBeast.Core.Diagnostics;
using CurseTheBeast.GUI.ViewModels;
using CurseTheBeast.GUI.Views;
using CurseTheBeast.GUI.Services;

namespace CurseTheBeast.GUI;

public partial class App : Application
{
    bool _subscribedToCoreLog;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            CoreLog.MessageEmitted += OnCoreLogMessage;
            _subscribedToCoreLog = true;
            desktop.Exit += OnDesktopExit;
            desktop.MainWindow = new MainWindow { DataContext = new MainWindowViewModel(), };
        }

        base.OnFrameworkInitializationCompleted();
    }

    void OnCoreLogMessage(CoreLogEntry entry)
    {
        LogStore.Add(entry.Level, entry.Source, entry.Message);
    }

    void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        if (_subscribedToCoreLog)
        {
            CoreLog.MessageEmitted -= OnCoreLogMessage;
            _subscribedToCoreLog = false;
        }
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
