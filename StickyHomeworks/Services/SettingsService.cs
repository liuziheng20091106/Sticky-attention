using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Hosting;
using StickyHomeworks.Models;
using System.ComponentModel;
using System.IO;
using System.Text.Json;

namespace StickyHomeworks.Services;

public class SettingsService : ObservableRecipient, IHostedService
{
    private Settings _settings = new();
    private System.Timers.Timer? _saveTimer;


    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await SaveSettingsAsync();
    }


    private void ScheduleSaveSettings()
    {
        _saveTimer?.Stop();
        _saveTimer = new System.Timers.Timer(500); // 延迟 500 毫秒
        _saveTimer.Elapsed += (sender, args) =>
        {
            SaveSettings();
            _saveTimer?.Dispose();
            _saveTimer = null;
        };
        _saveTimer.Start();
    }

    public SettingsService(IHostApplicationLifetime applicationLifetime)
    {
        PropertyChanged += OnPropertyChanged;
        Settings.PropertyChanged += (o, args) => OnSettingsChanged?.Invoke(o, args);
        LoadSettings();
        //applicationLifetime.ApplicationStopping.Register(SaveSettings);
        OnSettingsChanged += OnOnSettingsChanged;
    }

    private void OnOnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        ScheduleSaveSettings();
    }

    private async void LoadSettings()
    {
        if (!File.Exists("./Settings.json")) return;
        var json = await File.ReadAllTextAsync("./Settings.json");
        var r = JsonSerializer.Deserialize<Settings>(json);
        if (r != null)
        {
            Settings = r;
        }
    }

    public void SaveSettings()
    {
        File.WriteAllText("./Settings.json", JsonSerializer.Serialize<Settings>(Settings));
    }

    public event PropertyChangedEventHandler? OnSettingsChanged;

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Settings))
        {
            Settings.PropertyChanged += (o, args) => OnSettingsChanged?.Invoke(o, args);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        SaveSettings(); // 调用同步保存方法
        return Task.CompletedTask;
    }



    public async Task SaveSettingsAsync()
    {
        var json = JsonSerializer.Serialize(Settings);
        await File.WriteAllTextAsync("./Settings.json", json);
    }

    public Settings Settings
    {
        get => _settings;
        set
        {
            if (Equals(value, _settings)) return;
            _settings = value;
            OnPropertyChanged();
        }
    }
}