using ElysiaFramework;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;
using StickyHomeworks;
using StickyHomeworks.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace ClassIsland.Services;

public sealed class WallpaperPickingService : IHostedService, INotifyPropertyChanged
{
    private SettingsService SettingsService { get; }
    private static readonly string DesktopWindowClassName = "Progman";
    private ObservableCollection<Color> _wallpaperColorPlatte = new();
    private BitmapImage _wallpaperImage = new();
    private bool _isWorking;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RegistryNotifier RegistryNotifier { get; }
    private DispatcherTimer UpdateTimer { get; } = new()
    {
        Interval = TimeSpan.FromMinutes(1)
    };

    public WallpaperPickingService(SettingsService settingsService)
    {
        SettingsService = settingsService;
        SystemEvents.UserPreferenceChanged += SystemEventsOnUserPreferenceChanged;
        RegistryNotifier = new RegistryNotifier(RegistryNotifier.HKEY_CURRENT_USER, "Control Panel\\Desktop");
        RegistryNotifier.RegistryKeyUpdated += RegistryNotifierOnRegistryKeyUpdated;
        RegistryNotifier.Start();

        UpdateTimer.Tick += UpdateTimerOnTick;
        UpdateTimer.Interval = TimeSpan.FromSeconds(SettingsService.Settings.WallpaperAutoUpdateIntervalSeconds);
        SettingsService.Settings.PropertyChanged += SettingsServiceOnPropertyChanged;
        UpdateTimer.Start();
    }

    public event EventHandler? WallpaperColorPlatteChanged;

    public ObservableCollection<Color> WallpaperColorPlatte
    {
        get => _wallpaperColorPlatte;
        set
        {
            if (Equals(value, _wallpaperColorPlatte)) return;
            _wallpaperColorPlatte = value;
            OnPropertyChanged();
        }
    }

    public BitmapImage WallpaperImage
    {
        get => _wallpaperImage;
        set
        {
            if (Equals(value, _wallpaperImage)) return;
            _wallpaperImage = value;
            OnPropertyChanged();
        }
    }

    private void SettingsServiceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsService.Settings.WallpaperAutoUpdateIntervalSeconds))
        {
            UpdateTimer.Interval = TimeSpan.FromSeconds(SettingsService.Settings.WallpaperAutoUpdateIntervalSeconds);
        }
    }

    private async void UpdateTimerOnTick(object? sender, EventArgs e)
    {
        if (!SettingsService.Settings.IsWallpaperAutoUpdateEnabled)
        {
            return;
        }
        await GetWallpaperAsync();
    }

    private async void RegistryNotifierOnRegistryKeyUpdated()
    {
        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            await GetWallpaperAsync();
        });
    }

    private async void SystemEventsOnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.Desktop)
        {
            await GetWallpaperAsync();
        }
    }

    public static Bitmap? GetScreenShot(string className)
    {
        var win = NativeWindowHelper.FindWindowByClass(className);
        return win == IntPtr.Zero ? null : WindowCaptureHelper.CaptureWindow(win);
    }

    public static Bitmap? GetFallbackWallpaper()
    {
        try
        {
            var key = Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop");
            var path = key?.GetValue("WallPaper") as string;
            var screenBounds = Screen.PrimaryScreen.Bounds;
            return path == null ? null : new Bitmap(Image.FromFile(path), screenBounds.Width, screenBounds.Height);
        }
        catch
        {
            return null;
        }
    }

    public bool IsWorking
    {
        get => _isWorking;
        set
        {
            if (value == _isWorking) return;
            _isWorking = value;
            OnPropertyChanged();
        }
    }

    public async Task GetWallpaperAsync()
    {
        if (IsWorking) return;

        await _lock.WaitAsync();
        try
        {
            IsWorking = true;
            await Task.Run(() =>
            {
                var bitmap = SettingsService.Settings.IsFallbackModeEnabled
                    ? GetFallbackWallpaper()
                    : GetScreenShot(SettingsService.Settings.WallpaperClassName == ""
                        ? DesktopWindowClassName
                        : SettingsService.Settings.WallpaperClassName);

                if (bitmap is null) return;

                double dpiX = 1, dpiY = 1;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainWindow = (MainWindow)Application.Current.MainWindow!;
                    mainWindow.GetCurrentDpi(out dpiX, out dpiY);
                });

                WallpaperImage = BitmapConveters.ConvertToBitmapImage(bitmap, (int)(750 * dpiX));
                var colorList = ColorOctTreeNode.ProcessImage(bitmap)
                    .OrderByDescending(i =>
                    {
                        var color = (Color)ColorConverter.ConvertFromString(i.Key);
                        ColorToHsv(color, out _, out var s, out var v);
                        return s + v;
                    })
                    .ThenByDescending(i => i.Value)
                    .Take(5)
                    .Select(i => (Color)ColorConverter.ConvertFromString(i.Key))
                    .ToList();

                WallpaperColorPlatte.Clear();
                colorList.ForEach(c => WallpaperColorPlatte.Add(c));
            });

            WallpaperColorPlatteChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsWorking = false;
            _lock.Release();
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public static void ColorToHsv(Color color, out double hue, out double saturation, out double value)
    {
        int max = Math.Max(color.R, Math.Max(color.G, color.B));
        int min = Math.Min(color.R, Math.Min(color.G, color.B));

        hue = 0;
        saturation = (max == 0) ? 0 : 1d - (1d * min / max);
        value = max / 255d;
    }
}
