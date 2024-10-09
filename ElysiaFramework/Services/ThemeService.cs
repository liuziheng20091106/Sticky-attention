using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using ElysiaFramework.Interfaces;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;

namespace ElysiaFramework.Services;

public class ThemeService : IHostedService, IThemeService
{
    public event EventHandler<ThemeUpdatedEventArgs>? ThemeUpdated;
    public int CurrentRealThemeMode { get; set; } = 0; // 这里的 set 需要是 public

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // 这里可以进行主题初始化或其他启动逻辑
        await Task.CompletedTask; // 这里可能有未来的实现
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // 这里可以进行清理逻辑
        await Task.CompletedTask; // 这里可能有未来的实现
    }

    public void SetTheme(int themeMode, Color primary, Color secondary)
    {
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();

        // 记录上一个主题颜色
        var lastPrimary = theme.PrimaryMid.Color;
        var lastSecondary = theme.SecondaryMid.Color;
        var lastBaseTheme = theme.GetBaseTheme();

        // 根据主题模式选择主题
        SelectTheme(theme, themeMode);

        // 设置颜色调整
        SetColorAdjustment(theme);

        // 设置主色和次色
        theme.SetPrimaryColor(primary);
        theme.SetSecondaryColor(secondary);

        // 检查主题是否有变化
        if (HasThemeChanged(lastPrimary, lastSecondary, lastBaseTheme, paletteHelper.GetTheme()))
        {
            // 更新主题
            paletteHelper.SetTheme(theme);
            OnThemeUpdated(themeMode, primary, secondary);
        }
    }

    private void SelectTheme(ITheme theme, int themeMode)
    {
        switch (themeMode)
        {
            case 0:
                SetDynamicTheme(theme);
                break;
            case 1:
                theme.SetBaseTheme(new MaterialDesignLightTheme());
                break;
            case 2:
                theme.SetBaseTheme(new MaterialDesignDarkTheme());
                break;
        }
    }

    private void SetDynamicTheme(ITheme theme)
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize"))
            {
                if (key != null)
                {
                    var appsUseLightTheme = (int?)key.GetValue("AppsUseLightTheme");
                    theme.SetBaseTheme(appsUseLightTheme == 0 ? new MaterialDesignDarkTheme() : new MaterialDesignLightTheme());
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // 权限不足时使用默认主题
            theme.SetBaseTheme(new MaterialDesignLightTheme());
        }
        catch (Exception ex)
        {
            // 记录其他异常（可选）
            Console.WriteLine($"设置动态主题时发生错误: {ex.Message}");
            theme.SetBaseTheme(new MaterialDesignLightTheme());
        }
    }

    private void SetColorAdjustment(ITheme theme)
    {
        ((Theme)theme).ColorAdjustment = new ColorAdjustment
        {
            DesiredContrastRatio = 4.5F,
            Contrast = Contrast.Medium,
            Colors = ColorSelection.All
        };
    }

    private bool HasThemeChanged(Color lastPrimary, Color lastSecondary, BaseTheme lastBaseTheme, ITheme currentTheme)
    {
        return lastPrimary != currentTheme.PrimaryMid.Color ||
               lastSecondary != currentTheme.SecondaryMid.Color ||
               lastBaseTheme != currentTheme.GetBaseTheme();
    }

    private void OnThemeUpdated(int themeMode, Color primary, Color secondary)
    {
        ThemeUpdated?.Invoke(this, new ThemeUpdatedEventArgs
        {
            ThemeMode = themeMode,
            Primary = primary,
            Secondary = secondary,
            RealThemeMode = (CurrentRealThemeMode = (primary == Colors.White ? 0 : 1)) // 可根据实际逻辑更新
        });
    }
}
