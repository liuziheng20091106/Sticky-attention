using ClassIsland.Services;
using ElysiaFramework;
using ElysiaFramework.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Media;

namespace StickyHomeworks.Services
{
    public class ThemeBackgroundService : IHostedService
    {
        private SettingsService SettingsService { get; }
        private IThemeService ThemeService { get; }
        private WallpaperPickingService WallpaperPickingService { get; }
        private Stopwatch UpdateStopWatch { get; } = new();

        public ThemeBackgroundService(SettingsService settingsService, IThemeService themeService, WallpaperPickingService wallpaperPickingService)
        {
            SettingsService = settingsService;
            ThemeService = themeService;
            WallpaperPickingService = wallpaperPickingService;

            // 订阅事件
            SettingsService.OnSettingsChanged += SettingsServiceOnSettingsChanged;
            SystemEvents.UserPreferenceChanged += SystemEventsOnUserPreferenceChanged;
            WallpaperPickingService.WallpaperColorPlatteChanged += WallpaperPickingServiceOnWallpaperColorPlatteChanged;
        }

        private async void WallpaperPickingServiceOnWallpaperColorPlatteChanged(object? sender, EventArgs e)
        {
            await UpdateThemeAsync();
        }

        private async void SystemEventsOnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            await WallpaperPickingService.GetWallpaperAsync();
        }

        private async void SettingsServiceOnSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!WallpaperPickingService.IsWorking)
            {
                await UpdateThemeAsync();
            }
        }

        private async Task UpdateThemeAsync()
        {
            if (UpdateStopWatch.IsRunning && UpdateStopWatch.ElapsedMilliseconds < 300)
            {
                return;
            }
            UpdateStopWatch.Restart();

            var primary = Colors.DodgerBlue;
            var secondary = Colors.DodgerBlue;

            switch (SettingsService.Settings.ColorSource)
            {
                case 0: // 自定义颜色
                    primary = SettingsService.Settings.PrimaryColor;
                    secondary = SettingsService.Settings.SecondaryColor;
                    break;
                case 1: // 使用选定调色板
                    primary = secondary = SettingsService.Settings.SelectedPlatte;
                    break;
                case 2: // 动态系统颜色
                    try
                    {
                        NativeWindowHelper.DwmGetColorizationColor(out var color, out _);
                        primary = secondary = NativeWindowHelper.GetColor(color);
                    }
                    catch
                    {
                        // 忽略异常
                    }
                    break;
            }

            ThemeService.SetTheme(SettingsService.Settings.Theme, primary, secondary);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await WallpaperPickingService.GetWallpaperAsync();
            await UpdateThemeAsync();
            UpdateStopWatch.Start();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
