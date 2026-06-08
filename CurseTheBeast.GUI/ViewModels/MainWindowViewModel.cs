using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CurseTheBeast.Core.Api.FTB;
using CurseTheBeast.Core.Api.FTB.Model;
using CurseTheBeast.Core.Services;
using CurseTheBeast.GUI.Models;
using CurseTheBeast.GUI.Services;
using System.Collections.ObjectModel;

namespace CurseTheBeast.GUI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    public static readonly IReadOnlyList<string> ThemeModes =
    [
        "System",
        "Light",
        "Dark"
    ];
    public IReadOnlyList<string> AvailableThemeModes => ThemeModes;

    public static readonly IReadOnlyList<string> WindowTransparencyModes =
    [
        "AcrylicBlur",
        "Mica"
    ];
    public IReadOnlyList<string> AvailableWindowTransparencyModes => WindowTransparencyModes;
    public ObservableCollection<LogEntry> Logs => LogStore.Entries;

    readonly FTBApiClient _ftbClient;
    readonly FTBService _ftbService;
    readonly Dictionary<int, ModpackItemViewModel> _modpackCache = [];
    readonly Dictionary<int, ModpackInfo> _modpackDetailsCache = [];
    readonly AppSettings _settings;
    CancellationTokenSource? _requestCts;

    public ObservableCollection<ModpackItemViewModel> Results { get; } = [];
    public ObservableCollection<VersionItemViewModel> Versions { get; } = [];

    [ObservableProperty]
    string _searchKeyword = "";

    [ObservableProperty]
    string _statusText = "Ready for search modpack.";

    partial void OnStatusTextChanged(string value)
    {
        PublishStatus(value, nameof(StatusText));
    }
    
    [ObservableProperty]
    string _selectedDetailsName = "";
    
    [ObservableProperty]
    string _selectedDetailsId = "";
    
    [ObservableProperty]
    string _selectedDetailsAuthors = "";
    
    [ObservableProperty]
    string _selectedDetailsUpdatedTime = "";
    
    [ObservableProperty]
    string _selectedDetailsGameVersions = "";
    
    [ObservableProperty]
    string _selectedDetailsUrl = "";

    public bool HasSelectedDetails => !string.IsNullOrWhiteSpace(SelectedDetailsUrl);
    
    [ObservableProperty]
    string _selectedDetailsSynopsis = "";

    [ObservableProperty]
    string _downloadPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "CurseTheBeast.Downloads");

    [ObservableProperty]
    bool _downloadFullPack = true;

    [ObservableProperty]
    bool _loadAllOnStartup = true;

    [ObservableProperty]
    string _themeMode = "System";

    [ObservableProperty]
    string _windowTransparencyMode = "Default";

    [ObservableProperty]
    bool _isBusy;

    [ObservableProperty]
    ModpackItemViewModel? _selectedResult;

    [ObservableProperty]
    VersionItemViewModel? _selectedVersion;

    [ObservableProperty]
    string _updateDate = "";

    public IAsyncRelayCommand SearchCommand { get; }
    public IAsyncRelayCommand LoadAllCommand { get; }
    public IAsyncRelayCommand LoadFeaturedCommand { get; }
    public IRelayCommand ClearResultsCommand { get; }
    public IAsyncRelayCommand DownloadClientCommand { get; }
    public IAsyncRelayCommand DownloadServerCommand { get; }
    public IRelayCommand SaveSettingsCommand { get; }
    public IRelayCommand ResetSettingsCommand { get; }
    public IRelayCommand ClearLogsCommand { get; }

    public MainWindowViewModel()
    {
        _settings = AppSettingsStore.Load();
        ApplySettingsToView(_settings);
        _ftbClient = new FTBApiClient();
        _ftbService = new FTBService();

        SearchCommand = new AsyncRelayCommand(SearchAsync, () => !IsBusy);
        LoadAllCommand = new AsyncRelayCommand(LoadAllAsync, () => !IsBusy);
        LoadFeaturedCommand = new AsyncRelayCommand(LoadFeaturedAsync, () => !IsBusy);
        ClearResultsCommand = new RelayCommand(ClearResults, () => !IsBusy);
        DownloadClientCommand = new AsyncRelayCommand(() => DownloadAsync(false), CanDownload);
        DownloadServerCommand = new AsyncRelayCommand(() => DownloadAsync(true), CanDownload);
        SaveSettingsCommand = new RelayCommand(SaveSettings, () => !IsBusy);
        ResetSettingsCommand = new RelayCommand(ResetSettings, () => !IsBusy);
        ClearLogsCommand = new RelayCommand(LogStore.Clear);
        if (LoadAllOnStartup)
        {
            _ = LoadAllAsync();
        }
    }

    partial void OnSelectedResultChanged(ModpackItemViewModel? value)
    {
        _ = LoadSelectedModpackDetailsAsync(value);
        UpdateDownloadCommandState();
    }

    partial void OnSelectedVersionChanged(VersionItemViewModel? value)
    {
        UpdateDate = value?.UpdatedTime ?? "";
        UpdateDownloadCommandState();
    }

    partial void OnSelectedDetailsUrlChanged(string value)
    {
        OnPropertyChanged(nameof(HasSelectedDetails));
    }

    partial void OnIsBusyChanged(bool value)
    {
        SearchCommand.NotifyCanExecuteChanged();
        LoadAllCommand.NotifyCanExecuteChanged();
        LoadFeaturedCommand.NotifyCanExecuteChanged();
        ClearResultsCommand.NotifyCanExecuteChanged();
        UpdateDownloadCommandState();
        SaveSettingsCommand.NotifyCanExecuteChanged();
        ResetSettingsCommand.NotifyCanExecuteChanged();
    }

    async Task SearchAsync()
    {
        string keyword = SearchKeyword.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            await LoadAllAsync();
            return;
        }

        await ExecuteRequestAsync(async ct =>
        {
            var result = await _ftbClient.SearchAsync(keyword, ct);
            var items = result.packs.Select(MapFromSearch).ToArray();
            foreach (var item in items)
            {
                _modpackCache[item.Id] = item;
            }

            return items;
        }, $"Searching \"{keyword}\"...");
    }

    async Task LoadAllAsync()
    {
        await ExecuteRequestAsync(async ct =>
        {
            var listRsp = await _ftbClient.GetListAsync(ct);
            var list = new List<ModpackItemViewModel>(listRsp.packs.Length);
            var total = listRsp.packs.Length;
            for (var i = 0; i < total; i++)
            {
                ct.ThrowIfCancellationRequested();
                var id = listRsp.packs[i];
                if (_modpackCache.TryGetValue(id, out var cached))
                {
                    list.Add(cached);
                    continue;
                }

                StatusText = $"Loading all modpacks {i + 1}/{total}...";
                var info = await _ftbClient.GetInfoAsync(id, ct);
                var item = MapFromInfo(info);
                _modpackCache[id] = item;
                list.Add(item);
            }

            return list.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }, "Loading all modpacks...");
    }

    async Task LoadFeaturedAsync()
    {
        await ExecuteRequestAsync(async ct =>
        {
            var featured = await _ftbClient.GetFeaturedAsync(ct);
            var list = new List<ModpackItemViewModel>(featured.packs.Length);
            var total = featured.packs.Length;
            for (var i = 0; i < total; i++)
            {
                ct.ThrowIfCancellationRequested();
                StatusText = $"Loading featured {i + 1}/{total}...";
                var info = await _ftbClient.GetInfoAsync(featured.packs[i], ct);
                var item = MapFromInfo(info);
                _modpackCache[item.Id] = item;
                list.Add(item);
            }

            return list;
        }, "Loading featured modpacks...");
    }

    async Task LoadSelectedModpackDetailsAsync(ModpackItemViewModel? item)
    {
        if (item == null)
        {
            Versions.Clear();
            SelectedVersion = null;
            return;
        }

        try
        {
            var info = await FetchSelectedModpackDetailsAsync(item.Id);
            var authors = info.authors.Length == 0 ? "Unknown" : string.Join(", ", info.authors.Select(a => a.name));
            var extractedGameVersions = info.versions
                .SelectMany(v => v.targets)
                .Where(t => t.type.Equals("game", StringComparison.OrdinalIgnoreCase))
                .Select(t => t.version)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var gameVersions = extractedGameVersions.Length == 0 ? "Unknown" : string.Join(", ", extractedGameVersions);
            
            ApplyVersions(info);
            SelectedDetailsName = info.name;
            SelectedDetailsId = info.id.ToString();
            SelectedDetailsAuthors = authors;
            SelectedDetailsUpdatedTime = FormatUnixTime(info.updated);
            SelectedDetailsGameVersions = gameVersions;
            SelectedDetailsUrl = $"https://www.feed-the-beast.com/modpacks/{info.id}";
            SelectedDetailsSynopsis = string.IsNullOrWhiteSpace(info.synopsis) ? "N/A" : info.synopsis;
            StatusText = $"Ready. {Versions.Count} versions found.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Operation canceled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    async Task<ModpackInfo> FetchSelectedModpackDetailsAsync(int modpackId)
    {
        Versions.Clear();
        SelectedVersion = null;

        if (_modpackDetailsCache.TryGetValue(modpackId, out var cached))
        {
            StatusText = $"Loaded details for #{modpackId} from cache.";
            return cached;
        }

        CancelRequest();
        _requestCts = new CancellationTokenSource();
        var ct = _requestCts.Token;

        IsBusy = true;
        StatusText = $"Loading details for #{modpackId}...";

        var info = await _ftbService.GetModpackInfoAsync(modpackId, ct);
        _modpackDetailsCache[modpackId] = info;
        return info;
    }

    void ApplyVersions(ModpackInfo info)
    {
        foreach (var version in info.versions.OrderByDescending(v => v.updated))
        {
            Versions.Add(new VersionItemViewModel(
                version.id,
                version.name,
                version.type,
                FormatUnixTime(version.updated)));
        }

        SelectedVersion = Versions.FirstOrDefault();
    }

    async Task DownloadAsync(bool server)
    {
        if (!CanDownload())
        {
            StatusText = "Select modpack and version first.";
            return;
        }

        var selectedResult = SelectedResult!;
        var selectedVersion = SelectedVersion!;
        var dstPath = DownloadPath.Trim();
        if (string.IsNullOrWhiteSpace(dstPath))
        {
            StatusText = "Please input download path.";
            return;
        }

        CancelRequest();
        _requestCts = new CancellationTokenSource();
        var ct = _requestCts.Token;

        try
        {
            IsBusy = true;
            var mode = server ? "server" : "client";
            StatusText = $"Preparing {mode} package...";
            var pack = await _ftbService.GetModpackAsync(selectedResult.Id, selectedVersion.Id, ct);
            StatusText = $"Downloading to {dstPath}...";
            await _ftbService.DownloadModpackAsync(pack, server, DownloadFullPack, dstPath, ct);
            StatusText = $"Download completed: {dstPath}";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Operation canceled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Download failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    void ClearResults()
    {
        CancelRequest();
        Results.Clear();
        Versions.Clear();
        SelectedResult = null;
        SelectedVersion = null;
        StatusText = "Cleared.";
    }

    void SaveSettings()
    {
        _settings.DownloadPath = DownloadPath.Trim();
        _settings.DownloadFullPack = DownloadFullPack;
        _settings.LoadAllOnStartup = LoadAllOnStartup;
        _settings.ThemeMode = ThemeMode;
        _settings.WindowTransparencyMode = WindowTransparencyMode;

        AppSettingsStore.Save(_settings);
        StatusText = "Settings saved.";
    }

    void ResetSettings()
    {
        ApplySettingsToView(new AppSettings());
        StatusText = "Settings reset. Click Save Settings to apply.";
    }

    async Task ExecuteRequestAsync(
        Func<CancellationToken, Task<IReadOnlyList<ModpackItemViewModel>>> request,
        string loadingText)
    {
        CancelRequest();
        _requestCts = new CancellationTokenSource();
        var ct = _requestCts.Token;

        try
        {
            IsBusy = true;
            StatusText = loadingText;

            var data = await request(ct);
            Results.Clear();
            Versions.Clear();
            SelectedVersion = null;
            foreach (var item in data)
            {
                Results.Add(item);
            }

            SelectedResult = Results.FirstOrDefault();
            StatusText = $"Done. {Results.Count} result(s).";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Operation canceled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Request failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    bool CanDownload()
    {
        return !IsBusy && SelectedResult != null && SelectedVersion != null;
    }

    void UpdateDownloadCommandState()
    {
        DownloadClientCommand.NotifyCanExecuteChanged();
        DownloadServerCommand.NotifyCanExecuteChanged();
    }

    void ApplySettingsToView(AppSettings settings)
    {
        DownloadPath = settings.DownloadPath;
        DownloadFullPack = settings.DownloadFullPack;
        LoadAllOnStartup = settings.LoadAllOnStartup;
        ThemeMode = string.IsNullOrWhiteSpace(settings.ThemeMode) ? "System" : settings.ThemeMode;
        WindowTransparencyMode = string.IsNullOrWhiteSpace(settings.WindowTransparencyMode)
            ? "Default"
            : settings.WindowTransparencyMode;
    }

    static ModpackItemViewModel MapFromSearch(ModpackSearchResult.Pack pack)
    {
        var authors = pack.authors.Length == 0
            ? "Unknown"
            : string.Join(", ", pack.authors.Select(a => a.name));

        return new ModpackItemViewModel(
            pack.id,
            pack.name,
            pack.synopsis,
            authors,
            FormatUnixTime(pack.updated));
    }

    static ModpackItemViewModel MapFromInfo(ModpackInfo info)
    {
        var authors = info.authors.Length == 0
            ? "Unknown"
            : string.Join(", ", info.authors.Select(a => a.name));

        return new ModpackItemViewModel(
            info.id,
            info.name,
            info.synopsis,
            authors,
            FormatUnixTime(info.updated));
    }

    static string FormatUnixTime(int timestamp)
    {
        if (timestamp <= 0)
        {
            return "Unknown";
        }

        return DateTimeOffset.FromUnixTimeSeconds(timestamp).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }

    void CancelRequest()
    {
        if (_requestCts == null)
        {
            return;
        }

        try
        {
            _requestCts.Cancel();
        }
        finally
        {
            _requestCts.Dispose();
            _requestCts = null;
        }
    }

    public void Dispose()
    {
        CancelRequest();
        _ftbClient.Dispose();
        _ftbService.Dispose();
    }
}

public sealed class ModpackItemViewModel
{
    public int Id { get; }
    public string Name { get; }
    public string Synopsis { get; }
    public string Authors { get; }
    public string LastUpdated { get; }
    public string DisplayName => $"{Name} (#{Id})";

    public ModpackItemViewModel(int id, string name, string synopsis, string authors, string lastUpdated)
    {
        Id = id;
        Name = name;
        Synopsis = synopsis;
        Authors = authors;
        LastUpdated = lastUpdated;
    }
}

public sealed class VersionItemViewModel
{
    public int Id { get; }
    public string Name { get; }
    public string Type { get; }
    public string UpdatedTime { get; }
    public string DisplayName => $"{Name}[{Type}]";

    public VersionItemViewModel(int id, string name, string type, string updatedTime)
    {
        Id = id;
        Name = name;
        Type = type;
        UpdatedTime = updatedTime;
    }
}
