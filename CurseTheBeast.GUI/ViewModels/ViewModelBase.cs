using CommunityToolkit.Mvvm.ComponentModel;
using CurseTheBeast.GUI.Services;
using System.Diagnostics;

namespace CurseTheBeast.GUI.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    protected void PublishStatus(string statusText, string? source = null)
    {
        LogStore.Add(TraceEventType.Information, source ?? GetType().Name, statusText);
    }
}
