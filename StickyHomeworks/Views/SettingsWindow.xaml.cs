using ClassIsland.Services;
using ElysiaFramework;
using ElysiaFramework.Controls;
using MaterialDesignThemes.Wpf;
using StickyHomeworks.Models;
using StickyHomeworks.Services;
using StickyHomeworks.ViewModels;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace StickyHomeworks.Views;
/// <summary>
/// SettingsWindow.xaml 的交互逻辑
/// </summary>
public partial class SettingsWindow : MyWindow
{

    private const string UpdateUrl = "https://sticky-attention.sheng12.site/index.xml";
    private const string IconPath01 = "/Assets/icon/上传 (1).png"; // 有最新版本时的图标
    private const string IconPath02 = "/Assets/icon/成功 (2).png"; // 没有最新版本时的图标
    private const string IconPath03 = "/Assets/icon/叹号 (1).png"; // 有最新版本且下载完毕时的图标
    private const string DownloadFilePath = "update.zip";
    private const string DecompressionFolder = "Decompression update";

    public SettingsViewModel ViewModel
    {
        get;
        set;
    } = new();

    public Settings Settings
    {
        get;
        set;
    } = new();

    public bool IsOpened
    {
        get;
        set;
    } = false;

    public WallpaperPickingService WallpaperPickingService { get; }

    public SettingsWindow(WallpaperPickingService wallpaperPickingService,
        SettingsService settingsService)
    {
        WallpaperPickingService = wallpaperPickingService;

        InitializeComponent();
        DataContext = this;
        Settings = settingsService.Settings;
        settingsService.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == "Settings")
            {
                settingsService.Settings.PropertyChanged += SettingsOnPropertyChanged;
                Settings = settingsService.Settings;
            }
        };
        var style = (Style)FindResource("NotificationsListBoxItemStyle");
        //style.Setters.Add(new EventSetter(ListBoxItem.MouseDoubleClickEvent, new System.Windows.Input.MouseEventHandler(EventSetter_OnHandler)));
    }

    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {

    }


    protected override void OnInitialized(EventArgs e)
    {
        //RefreshMonitors();
        //var r = new StreamReader(Application.GetResourceStream(new Uri("/Assets/LICENSE.txt", UriKind.Relative))!.Stream);
        //ViewModel.License = r.ReadToEnd();
        base.OnInitialized(e);
    }

    protected override void OnContentRendered(EventArgs e)
    {
        Settings.PropertyChanged += SettingsOnPropertyChanged;
        base.OnContentRendered(e);
    }

    private void UIElement_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!e.Handled)
        {
            // ListView拦截鼠标滚轮事件
            e.Handled = true;

            // 激发一个鼠标滚轮事件，冒泡给外层ListView接收到
            var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
            eventArg.RoutedEvent = UIElement.MouseWheelEvent;
            eventArg.Source = sender;
            var parent = ((System.Windows.Controls.Control)sender).Parent as UIElement;
            if (parent != null)
            {
                parent.RaiseEvent(eventArg);
            }
        }
    }

    private void SettingsWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        AppEx.GetService<SettingsService>().SaveSettings();
        IsOpened = false;
    }

    private void ButtonCrash_OnClick(object sender, RoutedEventArgs e)
    {
        throw new Exception("Crash test.");
    }

    private void HyperlinkMsAppCenter_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo()
        {
            FileName = "https://learn.microsoft.com/zh-cn/appcenter/sdk/data-collected",
            UseShellExecute = true
        });
    }

    private void MyDrawerHost_OnDrawerClosing(object? sender, DrawerClosingEventArgs e)
    {
    }

    private void ButtonDebugToastText_OnClick(object sender, RoutedEventArgs e)
    {

    }


    private void ButtonDebugNetworkError_OnClick(object sender, RoutedEventArgs e)
    {
        //UpdateService.CurrentWorkingStatus = UpdateWorkingStatus.NetworkError;
    }


    private void OpenDrawer(string key)
    {
        MyDrawerHost.IsRightDrawerOpen = true;
        ViewModel.DrawerContent = FindResource(key);
    }

    private async Task<object?> ShowDialog(string key)
    {
        return await DialogHost.Show(FindResource(key), "SettingsWindow");
    }


    private void ButtonContributors_OnClick(object sender, RoutedEventArgs e)
    {
        OpenDrawer("ContributorsDrawer");
    }

    private void ButtonThirdPartyLibs_OnClick(object sender, RoutedEventArgs e)
    {
        OpenDrawer("ThirdPartyLibs");
    }

    private void AppIcon_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        ViewModel.AppIconClickCount++;
        if (ViewModel.AppIconClickCount >= 10)
        {
            Settings.IsDebugOptionsEnabled = true;
        }
    }

    private void ButtonCloseDebug_OnClick(object sender, RoutedEventArgs e)
    {
        Settings.IsDebugOptionsEnabled = false;
        ViewModel.AppIconClickCount = 0;
    }

    private void MenuItemDebugScreenShot_OnClick(object sender, RoutedEventArgs e)
    {

    }

    private async void ButtonUpdateWallpaper_OnClick(object sender, RoutedEventArgs e)
    {
        await WallpaperPickingService.GetWallpaperAsync();
    }

    private async void ButtonBrowseWindows_OnClick(object sender, RoutedEventArgs e)
    {
        var w = new WindowsPicker(Settings.WallpaperClassName)
        {
            Owner = this,
        };
        var r = w.ShowDialog();
        Settings.WallpaperClassName = w.SelectedResult ?? "";
        if (r == true)
        {
            await WallpaperPickingService.GetWallpaperAsync();
        }
        GC.Collect();
    }

    private void MenuItemExperimentalSettings_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsPopupMenuOpened = false;
        OpenDrawer("ExperimentalSettings");
    }

    private async Task EditSubjectAsync(int index)
    {
        ViewModel.SubjectEditText = Settings.Subjects[index];
        var r = (string?)await ShowDialog("EditSubjectDialog");
        if (r == null) return;
        Settings.Subjects[index] = r;
    }

    private async Task EditTagAsync(int index)
    {
        ViewModel.TagEditText = Settings.Tags[index];
        var r = (string?)await ShowDialog("EditTagDialog");
        if (r == null) return;
        Settings.Tags[index] = r;
    }

    private async void ButtonAddSubject_OnClick(object sender, RoutedEventArgs e)
    {
        Settings.Subjects.Add("");
        await EditSubjectAsync(Settings.Subjects.Count - 1);
        var r = Settings.Subjects.Last();
        if (r == "")
        {
            Settings.Subjects.RemoveAt(Settings.Subjects.Count - 1);
        }
        else
        {
            ViewModel.SubjectSelectedIndex = Settings.Subjects.Count - 1;
        }
    }

    private async void ButtonEditSubject_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SubjectSelectedIndex == -1)
        {
            return;
        }
        await EditSubjectAsync(ViewModel.SubjectSelectedIndex);
    }

    private void ButtonDeleteSubject_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SubjectSelectedIndex == -1)
        {
            return;
        }
        Settings.Subjects.RemoveAt(ViewModel.SubjectSelectedIndex);
    }

    private async void ButtonAddTag_OnClick(object sender, RoutedEventArgs e)
    {
        Settings.Tags.Add("");
        await EditTagAsync(Settings.Tags.Count - 1);
        var r = Settings.Tags.Last();
        if (r == "")
        {
            Settings.Tags.RemoveAt(Settings.Tags.Count - 1);
        }
        else
        {
            ViewModel.TagSelectedIndex = Settings.Tags.Count - 1;
        }
    }

    private async void ButtonEditTag_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TagSelectedIndex == -1)
        {
            return;
        }
        await EditTagAsync(ViewModel.TagSelectedIndex);
    }

    private void ButtonDeleteTag_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TagSelectedIndex == -1)
        {
            return;
        }
        Settings.Tags.RemoveAt(ViewModel.TagSelectedIndex);
    }

    private void MenuItemTestHomeworkEditWindow_OnClick(object sender, RoutedEventArgs e)
    {
        AppEx.GetService<HomeworkEditWindow>().Show();
    }

    private void OpenGitHub_Click(object sender, RoutedEventArgs e)
    {
        // 要打开的URL
        string url = "https://github.com/Sticky-attention/Sticky-attention/";

        // 使用默认浏览器打开URL
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            // 处理异常，例如无法打开浏览器的情况
            MessageBox.Show($"无法打开URL: {ex.Message}");
        }
    }

    private void IconText_Loaded(object sender, RoutedEventArgs e)
    {

    }

    private void IconText_Loaded_1(object sender, RoutedEventArgs e)
    {

    }

    private void Check_for_updates(object sender, RoutedEventArgs e)
    {
        // 显示进度条和标签
        pbDown.Visibility = Visibility.Visible;
        labelProgress.Visibility = Visibility.Visible;

        // 模拟异步检查更新
        var checkUpdateTask = Task.Run(() => CheckForUpdates());
    }

    private async void CheckForUpdates()
    {
        using (var client = new WebClient())
        {
            try
            {
                // 获取最新版本信息
                var xmlContent = await client.DownloadStringTaskAsync(new Uri(UpdateUrl));
                var updateInfo = ParseUpdateInfoFromXml(xmlContent);

                if (IsNewerVersion(updateInfo.Version, GetCurrentVersion()))
                {
                    // 有新版本
                    Dispatcher.Invoke(() =>
                    {
                        versionStatusTextBlock.Text = "检测到最新版本";
                        versionStatusTextBlock.FontSize = 40;
                        versionStatusTextBlock.FontWeight = FontWeights.Bold;
                        statusIcon.Source = new BitmapImage(new Uri(IconPath01, UriKind.Relative));
                    });

                    // 开始下载
                    await DownloadUpdate(client, updateInfo.Url);

                    // 下载完毕
                    Dispatcher.Invoke(() =>
                    {
                        versionStatusTextBlock.Text = "下载完成，请安装最新版本！";
                        statusIcon.Source = new BitmapImage(new Uri(IconPath03, UriKind.Relative));

                        var result = System.Windows.MessageBox.Show("您确定要运行更新程序吗？", "Sticky-attention", MessageBoxButton.YesNo, MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            // 用户点击了“是”，执行安装逻辑
                            // 解压ZIP文件
                            UnzipFile(DownloadFilePath, DecompressionFolder);

                            // 运行解压出来的Sticky-attention.exe
                            RunExecutableAndCloseApp(Path.Combine(DecompressionFolder, "Sticky-attention.exe"));

                            Close();
                        }
                        else
                        {
                            // 用户点击了“否”或关闭了消息框，不做任何事情
                            return;
                        }
                        // 隐藏进度条和标签
                        pbDown.Visibility = Visibility.Collapsed;
                        labelProgress.Visibility = Visibility.Collapsed;
                    });
                }
                else
                {
                    // 没有新版本
                    Dispatcher.Invoke(() =>
                    {
                        versionStatusTextBlock.Text = "您已是最新！";
                        versionStatusTextBlock.FontSize = 40;
                        versionStatusTextBlock.FontWeight = FontWeights.Bold;
                        statusIcon.Source = new BitmapImage(new Uri(IconPath02, UriKind.Relative));
                        MessageBox.Show("已经是最新版本！");
                        // 隐藏进度条和标签
                        pbDown.Visibility = Visibility.Collapsed;
                        labelProgress.Visibility = Visibility.Collapsed;
                    });
                }
            }
            catch (Exception ex)
            {
                // 处理异常
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"检查更新时发生错误: {ex.Message}");
                    // 隐藏进度条和标签
                    pbDown.Visibility = Visibility.Collapsed;
                    labelProgress.Visibility = Visibility.Collapsed;
                });
            }
        }
    }

    private async Task DownloadUpdate(WebClient client, string url)
    {
        client.DownloadProgressChanged += Client_DownloadProgressChanged;
        client.DownloadFileCompleted += Client_DownloadFileCompleted;

        // 开始下载
        await client.DownloadFileTaskAsync(new Uri(url), DownloadFilePath);
    }

    private void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
    {
        // 更新进度条
        Dispatcher.Invoke(() =>
        {
            pbDown.Value = e.ProgressPercentage;
            labelProgress.Content = $"{e.ProgressPercentage}%";
        });
    }

    private void Client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
    {
        if (e.Error != null)
        {
            // 处理错误
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"下载失败: {e.Error.Message}");
                // 隐藏进度条和标签
                pbDown.Visibility = Visibility.Collapsed;
                labelProgress.Visibility = Visibility.Collapsed;
            });
        }
        else
        {
            // 下载完成
        }
    }

    private void UnzipFile(string zipFilePath, string outputFolder)
    {
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
        }

        using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string fullPath = Path.Combine(outputFolder, entry.FullName);
                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(fullPath);
                }
                else
                {
                    entry.ExtractToFile(fullPath, true);
                }
            }
        }
    }

    private bool IsNewerVersion(string remoteVersion, string currentVersion)
    {
        return Version.Parse(remoteVersion) > Version.Parse(currentVersion);
    }

    private string GetCurrentVersion()
    {
        // 获取当前应用版本
        return App.AppVersion; // 这里应该替换为获取实际版本的方法
    }

    private (string Version, string Url, string Changelog, bool Mandatory) ParseUpdateInfoFromXml(string xmlContent)
    {
        XDocument doc = XDocument.Parse(xmlContent);
        var versionElement = doc.Root.Element("Version");
        var urlElement = doc.Root.Element("Url");
        var changelogElement = doc.Root.Element("Changelog");
        var mandatoryElement = doc.Root.Element("Mandatory");

        string version = versionElement?.Value ?? "0.0.0";
        string url = urlElement?.Value ?? string.Empty;
        string changelog = changelogElement?.Value ?? string.Empty;
        bool mandatory = bool.TryParse(mandatoryElement?.Value, out bool isMandatory) && isMandatory;

        return (version, url, changelog, mandatory);
    }

    private void RunExecutableAndCloseApp(string filePath)
    {
        try
        {
            // 关闭当前应用程序
            Application.Current.Shutdown();

            // 启动新的可执行文件
            Process.Start(filePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法启动程序: {ex.Message}");
        }
    }

}

