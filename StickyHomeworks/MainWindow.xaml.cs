using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ElysiaFramework;
using MaterialDesignThemes.Wpf;
using StickyHomeworks.Models;
using StickyHomeworks.Services;
using StickyHomeworks.ViewModels;
using StickyHomeworks.Views;
using System.Windows.Forms;
using System.Windows.Threading;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using System.Drawing;
namespace StickyHomeworks;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private PropertyChangedEventHandler ViewModelOnPropertyChanged;

    public MainViewModel ViewModel { get; set; } = new MainViewModel();

    public ProfileService ProfileService { get; }

    public SettingsService SettingsService { get; }

    public event EventHandler? OnHomeworkEditorUpdated;

    public MainWindow(ProfileService profileService,
                      SettingsService settingsService,
                      WindowFocusObserverService focusObserverService)
    {
        ProfileService = profileService;
        SettingsService = settingsService;
        //Automation.AddAutomationFocusChangedEventHandler(OnFocusChangedHandler);
        InitializeComponent();
        focusObserverService.FocusChanged += FocusObserverServiceOnFocusChanged;
        ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
        ViewModel.PropertyChanging += ViewModelOnPropertyChanging;
        DataContext = this;
    }

    private void FocusObserverServiceOnFocusChanged(object? sender, EventArgs e)
    {
        if (!ViewModel.IsDrawerOpened)
            return;
        try
        {
            var hWnd = NativeWindowHelper.GetForegroundWindow();
            NativeWindowHelper.GetWindowThreadProcessId(hWnd, out var id);
            using var proc = Process.GetProcessById(id);
            if (proc.Id != Environment.ProcessId &&
                !new List<string>(["ctfmon", "textinputhost", "chsime"]).Contains(proc.ProcessName.ToLower()))
            {
                Dispatcher.Invoke(() => ExitEditingMode());
            }
        }
        catch
        {
            // ignored
        }
    }

    private void ViewModelOnPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.SelectedHomework))
        {
            ExitEditingMode(true);
        }
    }

    //private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    //{
    //    if (e.PropertyName == nameof(ViewModel.SelectedListBoxItem))
    //    {
    //        RepositionEditingWindow();
    //    }
    //    if (e.PropertyName == nameof(ViewModel.SelectedHomework))
    //    {
    //        ExitEditingMode(false);
    //    }
    //}
    

    private void ExitEditingMode(bool hard=true)
    {
        if (ViewModel.IsCreatingMode)
        {
            ViewModel.IsCreatingMode = false;
            return;
        }
        if (hard)
            MainListView.SelectedIndex = -1;
        ViewModel.IsDrawerOpened = false;
        AppEx.GetService<HomeworkEditWindow>().TryClose();
        AppEx.GetService<ProfileService>().SaveProfile();
    }

    private void SetPos()
    {
        GetCurrentDpi(out var dpi, out _);
        Left = SettingsService.Settings.WindowX / dpi;
        Top = SettingsService.Settings.WindowY / dpi;
        Width = SettingsService.Settings.WindowWidth / dpi;
        Height = SettingsService.Settings.WindowHeight / dpi;
    }

    private void SavePos()
    {
        GetCurrentDpi(out var dpi, out _);
        SettingsService.Settings.WindowX = Left * dpi;
        SettingsService.Settings.WindowY = Top * dpi;
        if (ViewModel.IsExpanded)
        {
            SettingsService.Settings.WindowWidth = Width * dpi;
            SettingsService.Settings.WindowHeight = Height * dpi;
        }
    }

    protected override void OnInitialized(EventArgs e)
    {
        ViewModel.ExpiredHomeworks = ProfileService.CleanupOutdated();
        if (ViewModel.ExpiredHomeworks.Count > 0)
        {
            ViewModel.CanRecoverExpireHomework = true;
            ViewModel.SnackbarMessageQueue.Enqueue($"清除了{ViewModel.ExpiredHomeworks.Count}条过期的作业。",
                "恢复", (o) => { RecoverExpiredHomework(); }, null, false, false, TimeSpan.FromSeconds(20));
        }
        base.OnInitialized(e);
    }

    private void RecoverExpiredHomework()
    {
        if (!ViewModel.CanRecoverExpireHomework)
            return;
        ViewModel.CanRecoverExpireHomework = false;
        var rm = ViewModel.ExpiredHomeworks;
        foreach (var i in rm)
        {
            ProfileService.Profile.Homeworks.Add(i);
        }
    }

    protected override void OnContentRendered(EventArgs e)
    {
        SetBottom();
        SetPos();
        AppEx.GetService<HomeworkEditWindow>().EditingFinished += OnEditingFinished;
        AppEx.GetService<HomeworkEditWindow>().SubjectChanged += OnSubjectChanged;
        base.OnContentRendered(e);
    }

    private void OnSubjectChanged(object? sender, EventArgs e)
    {
        if (ViewModel.IsUpdatingHomeworkSubject)
            return;
        if (ViewModel.SelectedHomework == null)
            return;
        if (!ViewModel.IsDrawerOpened)
            return;
        ViewModel.IsUpdatingHomeworkSubject = true;
        var s = ViewModel.SelectedHomework;
        ProfileService.Profile.Homeworks.Remove(s);
        ProfileService.Profile.Homeworks.Add(s);
        ViewModel.SelectedHomework = s;
        ViewModel.IsUpdatingHomeworkSubject = false;
    }

    private void OnEditingFinished(object? sender, EventArgs e)
    {
        ExitEditingMode();
    }

    private void ButtonCreateHomework_OnClick(object sender, RoutedEventArgs e)
    {
        CreateHomework();
    }

    private void CreateHomework()
    {
        ViewModel.IsUpdatingHomeworkSubject = true;
        OnHomeworkEditorUpdated?.Invoke(this ,EventArgs.Empty);
        var lastSubject = ViewModel.EditingHomework.Subject;
        ViewModel.IsCreatingMode = true;
        ViewModel.IsDrawerOpened = true;
        var o = new Homework()
        {
            Subject = lastSubject
        };
        ViewModel.EditingHomework = o;
        ViewModel.SelectedHomework = o;
        ProfileService.Profile.Homeworks.Add(o);
        //ComboBoxSubject.Text = lastSubject;
        SettingsService.SaveSettings();
        ProfileService.SaveProfile();
        ViewModel.IsUpdatingHomeworkSubject = false;
        RepositionEditingWindow();
        AppEx.GetService<HomeworkEditWindow>().TryOpen();
    }

    private void ButtonAddHomeworkCompleted_OnClick(object sender, RoutedEventArgs e)
    {
        ProfileService.Profile.Homeworks.Add(ViewModel.EditingHomework);
        ViewModel.IsDrawerOpened = false;
    }

    public void GetCurrentDpi(out double dpiX, out double dpiY)
    {
        var source = PresentationSource.FromVisual(this);

        dpiX = 1.0;
        dpiY = 1.0;

        if (source?.CompositionTarget != null)
        {
            dpiX = 1.0 * source.CompositionTarget.TransformToDevice.M11;
            dpiY = 1.0 * source.CompositionTarget.TransformToDevice.M22;
        }
    }

    private void ButtonSettings_OnClick(object sender, RoutedEventArgs e)
    {
        OpenSettingsWindow();
    }

    private void OpenSettingsWindow()
    {
        var win = AppEx.GetService<SettingsWindow>();
        if (!win.IsOpened)
        {
            //Analytics.TrackEvent("打开设置窗口");
            win.IsOpened = true;
            win.Show();
        }
        else
        {
            if (win.WindowState == WindowState.Minimized)
            {
                win.WindowState = WindowState.Normal;
            }

            win.Activate();
        }
    }

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (!ViewModel.IsClosing)
        {
            e.Cancel = true;
            return;
        }

        SavePos();
        SettingsService.SaveSettings();
        ProfileService.SaveProfile();
    }

    private void ButtonEditTags_OnClick(object sender, RoutedEventArgs e)
    {
        OnHomeworkEditorUpdated?.Invoke(this, EventArgs.Empty);
        ViewModel.IsTagEditingPopupOpened = true;
    }

  private void ButtonEditHomework_OnClick(object sender, RoutedEventArgs e)
    {
        OnHomeworkEditorUpdated?.Invoke(this, EventArgs.Empty);
        ViewModel.IsCreatingMode = false;
        if (ViewModel.SelectedHomework == null)
            return;
        ViewModel.EditingHomework = ViewModel.SelectedHomework;
        ViewModel.IsDrawerOpened = true;
        RepositionEditingWindow();
        AppEx.GetService<HomeworkEditWindow>().TryOpen();
    }



    private void ButtonRemoveHomework_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsUpdatingHomeworkSubject = true;
        if (ViewModel.SelectedHomework == null)
            return;
        ProfileService.Profile.Homeworks.Remove(ViewModel.SelectedHomework);
        ViewModel.IsUpdatingHomeworkSubject = false;
    }

    private void ButtonEditDone_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsDrawerOpened = false;
    }

    private void DragBorder_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.IsUnlocked && e.LeftButton == MouseButtonState.Pressed)
        {
            SetBottom();
            DragMove();
            SetBottom();
        }
    }

    private void SetBottom()
    {
        if (!SettingsService.Settings.IsBottom)
        {
            return;
        }
        var hWnd = new WindowInteropHelper(this).Handle;
        NativeWindowHelper.SetWindowPos(hWnd, NativeWindowHelper.HWND_BOTTOM, 0, 0, 0, 0, NativeWindowHelper.SWP_NOSIZE | NativeWindowHelper.SWP_NOMOVE | NativeWindowHelper.SWP_NOACTIVATE);
    }

    private void MainWindow_OnStateChanged(object? sender, EventArgs e)
    {
        SetBottom();
    }

    private void MainWindow_OnActivated(object? sender, EventArgs e)
    {
        SetBottom();
    }

    private void ButtonExit_OnClick(object sender, RoutedEventArgs e)
    {
        // 显示一个消息框询问用户是否要重启应用程序
        var result = System.Windows.MessageBox.Show("您确定要关闭程序吗？", "Sticky-attention", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            // 用户点击了“是”，执行重启逻辑
            // 通常来说，重启应用需要重新启动进程。
            // 这里假设你有一个方法RestartApplication()来处理重启逻辑。
            ViewModel.IsClosing = true;
            Close();
        }
        else
        {
            // 用户点击了“否”或关闭了消息框，不做任何事情
            return;
        }
    }

    private void ButtonDateSetToday_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.EditingHomework.DueTime = DateTime.Today;
    }

    private void ButtonDateSetWeekends_OnClick(object sender, RoutedEventArgs e)
    {
        var today = DateTime.Today;
        var delta = DayOfWeek.Saturday - today.DayOfWeek + 1;
        ViewModel.EditingHomework.DueTime = today + TimeSpan.FromDays(delta);
    }

    private void ButtonExpandingSwitcher_OnClick(object sender, RoutedEventArgs e)
    {
        SavePos();
        ViewModel.IsExpanded = !ViewModel.IsExpanded;
        if (ViewModel.IsExpanded)
        {
            SizeToContent = SizeToContent.Manual;
            SetPos();
        }
        else
        {
            ViewModel.IsUnlocked = false;
            SizeToContent = SizeToContent.Height;
            Width = Math.Min(ActualWidth, 350);
        }
    }

    private void MainWindow_OnDeactivated(object? sender, EventArgs e)
    {
        //MainListView.SelectedIndex = -1;
    }

    private async void ButtonExport_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsWorking = true;
        var dialog = new System.Windows.Forms.SaveFileDialog()
        {
            Filter = "图片 (*.png)|*.png"
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            goto done;
        }

        ExitEditingMode();
        //MainListView.Background = (Brush)FindResource("MaterialDesignPaper");
        await System.Windows.Threading.Dispatcher.Yield(DispatcherPriority.Render);
        var file = dialog.FileName!;
        var visual = new DrawingVisual();
        var s = SettingsService.Settings.Scale;
        using (var context = visual.RenderOpen())
        {
            var brush = new VisualBrush(MainListView)
            {
                Stretch = Stretch.None
            };
            var bg = (System.Windows.Media.Brush)FindResource("MaterialDesignPaper");
            context.DrawRectangle(bg, null, new Rect(0, 0, MainListView.ActualWidth * s, MainListView.ActualHeight * s)); 
            context.DrawRectangle(brush, null, new Rect(0, 0, MainListView.ActualWidth * s, MainListView.ActualHeight * s));
            context.Close();
        }

        var bitmap = new RenderTargetBitmap((int)(MainListView.ActualWidth * s), (int)(ActualHeight * s), 96d, 96d,
            PixelFormats.Default);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        try
        {
            var stream = File.Open(file, FileMode.OpenOrCreate);
            encoder.Save(stream);
            stream.Close();
            ViewModel.SnackbarMessageQueue.Enqueue($"成功地导出到：{file}", "查看", () =>
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = file,
                    UseShellExecute = true
                });
            });

        }
        catch(Exception ex)
        {
            ViewModel.SnackbarMessageQueue.Enqueue($"导出失败：{ex}");
        }

        done:
        //MainListView.Background = null;
        dialog.Dispose();
        ViewModel.IsWorking = false;
    }

    private void DrawerHost_OnDrawerClosing(object? sender, DrawerClosingEventArgs e)
    {
        SettingsService.SaveSettings();
        ProfileService.SaveProfile();
    }

    private void ButtonMore_Click(object sender, RoutedEventArgs e)
    {
        PopupExAdvanced.IsOpen = true;
    }

    private void MainListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        //ExitEditingMode(false);
    }

    public void OnTextBoxEnter()
    {
        CreateHomework();
    }

    private void MenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        PopupExAdvanced.IsOpen = false;
    }

    private void MenuItemRecoverExpiredHomework_OnClick(object sender, RoutedEventArgs e)
    {
        RecoverExpiredHomework();
    }

    private void ButtonRestart_OnClick(object sender, RoutedEventArgs e)
    {
        App.ReleaseLock();
        System.Windows.Forms.Application.Restart();
        System.Windows.Application.Current.Shutdown();
    }

    private void MainWindow_OnDragOver(object sender, DragEventArgs e)
    {

    }

    private void MainWindow_OnDragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;
        ViewModel.IsExpanded = false;
        ViewModel.IsUnlocked = false;
        SizeToContent = SizeToContent.Height;
        Width = Math.Min(ActualWidth, 350);
    }

    private async void RepositionEditingWindow()
    {
        if (ViewModel.SelectedListBoxItem == null)
        {
            Debug.WriteLine("SelectedListBoxItem is null, cannot reposition the editing window.");
            return;
        }

        try
        {
            // 获取当前屏幕的DPI
            GetCurrentDpi(out var dpiX, out var dpiY);

            // 将选定ListBoxItem的右上角坐标转换为屏幕坐标系下的点
            var listBoxItemPoint = ViewModel.SelectedListBoxItem.PointToScreen(new System.Windows.Point(ViewModel.SelectedListBoxItem.ActualWidth, 0));

            // 将WPF的Point转换为GDI+的Point
            var gdiPoint = new System.Drawing.Point((int)listBoxItemPoint.X, (int)listBoxItemPoint.Y);

            // 获取包含该点的屏幕
            var screen = System.Windows.Forms.Screen.FromPoint(gdiPoint);
            var workingArea = screen.WorkingArea;

            // 通过服务提供者获取HomeworkEditWindow实例
            var homeworkEditWindow = AppEx.GetService<HomeworkEditWindow>();

            // 确保窗口已经初始化
            if (homeworkEditWindow == null || !homeworkEditWindow.IsInitialized)
            {
                Debug.WriteLine("HomeworkEditWindow is not initialized, cannot reposition the editing window.");
                return;
            }

            // 如果窗口尚未加载完成，等待其加载
            if (!homeworkEditWindow.IsLoaded)
            {
                await Task.Run(() =>
                {
                    homeworkEditWindow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Wait();
                });
            }

            // 计算窗口的位置
            double left = listBoxItemPoint.X / dpiX;
            double top = Math.Min(listBoxItemPoint.Y, workingArea.Bottom - homeworkEditWindow.ActualHeight * dpiY) / dpiY;

            // 确保窗口完全在屏幕上
            left = Math.Max(workingArea.Left, Math.Min(left, workingArea.Right - homeworkEditWindow.ActualWidth));
            top = Math.Max(workingArea.Top, Math.Min(top, workingArea.Bottom - homeworkEditWindow.ActualHeight));

            // 设置窗口的位置
            homeworkEditWindow.Left = left;
            homeworkEditWindow.Top = top;

            Debug.WriteLine($"Repositioned HomeworkEditWindow to: Left={left}, Top={top}");
        }
        catch (Exception e)
        {
            // 处理可能发生的异常
            Debug.WriteLine($"Error repositioning the editing window: {e.Message}");
        }
    }
}