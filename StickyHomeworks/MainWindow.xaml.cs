using ElysiaFramework;
using MaterialDesignThemes.Wpf;
using StickyHomeworks.Models;
using StickyHomeworks.Services;
using StickyHomeworks.ViewModels;
using StickyHomeworks.Views;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
namespace StickyHomeworks
{
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

        string folderName = "备份";

        // 获取当前应用程序的执行目录
        string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;


        public MainWindow(ProfileService profileService,
                          SettingsService settingsService,
                          WindowFocusObserverService focusObserverService)
        {
            ProfileService = profileService;
            SettingsService = settingsService;
            // 注册自动化焦点变化事件处理器
            //Automation.AddAutomationFocusChangedEventHandler(OnFocusChangedHandler);
            InitializeComponent();
            // 注册焦点变化事件
            focusObserverService.FocusChanged += FocusObserverServiceOnFocusChanged;
            // 注册 ViewModel 属性变化事件
            ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
            ViewModel.PropertyChanging += ViewModelOnPropertyChanging;
            // 设置窗口的数据上下文为当前窗口实例
            DataContext = this;
            // 注册窗口关闭事件（可能无效）
            Closing +=  OnApplicationExit;
            focusObserverService.FocusChanged += FocusObserverServiceOnFocusChanged;
            ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
            ViewModel.PropertyChanging += ViewModelOnPropertyChanging;
            DataContext = this;
            Application.Current.Exit += OnApplicationExits;
        }

        //1.事件处理器来保存窗口位置 防止用户手动从任务管理器关闭软件而导致的无法保存位置（可能无效）
        private void OnApplicationExit(object sender, CancelEventArgs e)
        {
            // 保存窗口位置
            SavePos();
            // 保存设置
            SettingsService.SaveSettings();
            // 保存用户配置文件
            ProfileService.SaveProfile();
        }
        //2.事件处理器来保存窗口位置 防止用户手动从任务管理器关闭软件而导致的无法保存位置（可能无效）
        private void OnApplicationExits(object sender, ExitEventArgs e)
        {
            // 保存窗口位置
            SavePos();
            // 保存设置
            SettingsService.SaveSettings();
            // 保存用户配置文件
            ProfileService.SaveProfile();
        }


        private void FocusObserverServiceOnFocusChanged(object? sender, EventArgs e)
        {
            // 当抽屉未打开时直接返回
            if (!ViewModel.IsDrawerOpened)
                return;
            try
            {
                // 获取当前活动窗口的句柄
                var hWnd = NativeWindowHelper.GetForegroundWindow();
                // 获取当前窗口的进程 ID
                NativeWindowHelper.GetWindowThreadProcessId(hWnd, out var id);
                using var proc = Process.GetProcessById(id);
                // 如果当前进程不是应用进程，并且进程名不是特定列表中的名称，则退出编辑模式
                if (proc.Id != Environment.ProcessId &&
                    !new List<string>(["ctfmon", "textinputhost", "chsime"]).Contains(proc.ProcessName.ToLower()))
                {
                    Dispatcher.Invoke(() => ExitEditingMode());
                }
            }
            catch
            {
                // 捕获并忽略异常
            }
        }

        private void ViewModelOnPropertyChanging(object? sender, PropertyChangingEventArgs e)
        {
            // 如果属性名称是 SelectedHomework，准备退出编辑模式
            if (e.PropertyName == nameof(ViewModel.SelectedHomework))
            {
                ExitEditingMode(true);
            }
        }

        private void ExitEditingMode(bool hard = true)
        {
            // 如果当前处于创建模式，则退出创建模式
            if (ViewModel.IsCreatingMode)
            {
                ViewModel.IsCreatingMode = false;
                return;
            }
            // 如果 hard 参数为 true，则将 MainListView 的选中索引设置为 -1
            if (hard)
                MainListView.SelectedIndex = -1;
            // 关闭抽屉
            ViewModel.IsDrawerOpened = false;
            // 尝试关闭作业编辑窗口
            AppEx.GetService<HomeworkEditWindow>().TryClose();
            // 保存用户配置文件
            AppEx.GetService<ProfileService>().SaveProfile();
        }

        private void SetPos()
        {
            // 获取当前 DPI
            GetCurrentDpi(out var dpi, out _);
            // 根据保存的窗口位置和当前 DPI 设置窗口的位置
            Left = SettingsService.Settings.WindowX / dpi;
            Top = SettingsService.Settings.WindowY / dpi;
            Width = SettingsService.Settings.WindowWidth / dpi;
            Height = SettingsService.Settings.WindowHeight / dpi;
        }

        private void SavePos()
        {
            // 获取当前 DPI
            GetCurrentDpi(out var dpi, out _);
            // 根据当前窗口的位置和尺寸以及 DPI 保存设置
            SettingsService.Settings.WindowX = Left * dpi;
            SettingsService.Settings.WindowY = Top * dpi;
            if (ViewModel.IsExpanded)
            {
                SettingsService.Settings.WindowWidth = Width * dpi;
                SettingsService.Settings.WindowHeight = Height * dpi;
            }
        }

        protected void OnInitialized(EventArgs e)
        {
            // 初始化时清理过期作业
            ViewModel.ExpiredHomeworks = ProfileService.CleanupOutdated();
            if (ViewModel.ExpiredHomeworks.Count > 0)
            {
                ViewModel.CanRecoverExpireHomework = true;
                // 如果有过期作业，显示提示信息，并提供恢复选项（误了）
            }
            base.OnInitialized(e);
        }

        private void RecoverExpiredHomework()
        {
            // 恢复过期作业
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
            // 设置窗口置底，并设置窗口的位置
            SetBottom();
            SetPos();
            // 注册编辑完成和主题更改事件
            AppEx.GetService<HomeworkEditWindow>().EditingFinished += OnEditingFinished;
            AppEx.GetService<HomeworkEditWindow>().SubjectChanged += OnSubjectChanged;
            base.OnContentRendered(e);
        }

        private void OnSubjectChanged(object? sender, EventArgs e)
        {
            // 当作业主题更改时，更新作业列表
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
            // 编辑完成时退出编辑模式
            ExitEditingMode();
            AutoExport(null, null);
        }

        private void ButtonCreateHomework_OnClick(object sender, RoutedEventArgs e)
        {
            // 点击创建作业按钮时，调用创建作业的方法
            CreateHomework();
        }

        private void CreateHomework()
        {
            // 开始创建作业
            ViewModel.IsUpdatingHomeworkSubject = true;
            OnHomeworkEditorUpdated?.Invoke(this, EventArgs.Empty);
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
            SettingsService.SaveSettings();
            ProfileService.SaveProfile();
            ViewModel.IsUpdatingHomeworkSubject = false;
            RepositionEditingWindow();
            AppEx.GetService<HomeworkEditWindow>().TryOpen();
        }

        private void ButtonAddHomeworkCompleted_OnClick(object sender, RoutedEventArgs e)
        {
            // 完成添加作业
            ProfileService.Profile.Homeworks.Add(ViewModel.EditingHomework);
            ViewModel.IsDrawerOpened = false;
        }

        public void GetCurrentDpi(out double dpiX, out double dpiY)
        {
            // 获取当前视觉对象的 DPI 值
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
            // 点击设置按钮，打开设置窗口
            OpenSettingsWindow();
        }

        private void OpenSettingsWindow()
        {
            // 获取设置窗口服务
            var win = AppEx.GetService<SettingsWindow>();
            if (!win.IsOpened)
            {
                // 如果设置窗口未开启，则开启它
                win.IsOpened = true;
                win.Show();
            }
            else
            {
                // 如果设置窗口已开启但最小化，则恢复它
                if (win.WindowState == WindowState.Minimized)
                {
                    win.WindowState = WindowState.Normal;
                }

                // 激活设置窗口
                win.Activate();
            }
        }

        private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
        {
            // 窗口关闭事件处理
            if (!ViewModel.IsClosing)
            {
                e.Cancel = true;
                return;
            }
            AutoExport(null, null);
            // 保存窗口位置
            SavePos();
            // 保存设置
            SettingsService.SaveSettings();
            // 保存用户配置文件
            ProfileService.SaveProfile();
        }

        private void ButtonEditHomework_OnClick(object sender, RoutedEventArgs e)
        {
            // 点击编辑作业按钮，触发编辑事件
            OnHomeworkEditorUpdated?.Invoke(this, EventArgs.Empty);
            ViewModel.IsCreatingMode = false;

            if (ViewModel.SelectedHomework == null)
                return;

            ViewModel.EditingHomework = ViewModel.SelectedHomework;
            ViewModel.IsDrawerOpened = true;

            // 获取 HomeworkEditWindow 的实例并设置窗口位置
            var editWindow = AppEx.GetService<HomeworkEditWindow>();
            editWindow.ShowAtMousePosition(); // 在鼠标右侧打开窗口
        }



        private void ButtonRemoveHomework_OnClick(object sender, RoutedEventArgs e)
        {
            // 点击移除作业按钮，移除选中的作业
            ViewModel.IsUpdatingHomeworkSubject = true;
            if (ViewModel.SelectedHomework == null)
                return;
            ProfileService.Profile.Homeworks.Remove(ViewModel.SelectedHomework);
            ViewModel.IsUpdatingHomeworkSubject = false;
            AutoExport(null, null);
        }

        private void ButtonEditDone_OnClick(object sender, RoutedEventArgs e)
        {
            // 点击编辑完成按钮，关闭抽屉
            ViewModel.IsDrawerOpened = false;
            AutoExport(null, null);
        }

        private void DragBorder_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 当在可拖动边框上按下鼠标时，如果窗口未锁定，则开始拖动窗口
            if (ViewModel.IsUnlocked && e.LeftButton == MouseButtonState.Pressed)
            {
                SetBottom();
                DragMove();
                SetBottom();
                SavePos();//调取当前位置
                // 保存设置
                SettingsService.SaveSettings();
                // 保存用户配置文件
                ProfileService.SaveProfile();
            }
        }

        private void SetBottom()
        {
            // 如果设置中指定窗口应置于底部，则调用 SetWindowPos 方法将窗口置底
            if (!SettingsService.Settings.IsBottom)
            {
                return;
            }
            var hWnd = new WindowInteropHelper(this).Handle;
            NativeWindowHelper.SetWindowPos(hWnd, NativeWindowHelper.HWND_BOTTOM, 0, 0, 0, 0, NativeWindowHelper.SWP_NOSIZE | NativeWindowHelper.SWP_NOMOVE | NativeWindowHelper.SWP_NOACTIVATE);
        }

        private void MainWindow_OnStateChanged(object? sender, EventArgs e)
        {
            // 当窗口状态改变时，如果窗口被最大化或调整大小，调用 SetBottom 方法
            SetBottom();
            SavePos();
            // 保存设置
            SettingsService.SaveSettings();
            // 保存用户配置文件
            ProfileService.SaveProfile();
        }

        private void MainWindow_OnActivated(object? sender, EventArgs e)
        {
            // 当窗口被激活时，调用 SetBottom 方法
            SetBottom();
            SavePos();
            // 保存设置
            SettingsService.SaveSettings();
            // 保存用户配置文件
            ProfileService.SaveProfile();
        }

        private void ButtonExit_OnClick(object sender, RoutedEventArgs e)
        {
            // 显示一个消息框询问用户是否要关闭程序
            AutoExport(null, null);
            var result = System.Windows.MessageBox.Show("您确定要关闭程序吗？", "Sticky-attention", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                // 如果用户选择“是”，则执行关闭逻辑
                ViewModel.IsClosing = true;
                Close();
            }
            else
            {
                // 如果用户选择“否”，则不执行任何操作
                return;
            }
        }

        private void ButtonDateSetToday_OnClick(object sender, RoutedEventArgs e)
        {
            // 设置编辑中的作业的截止日期为今天
            ViewModel.EditingHomework.DueTime = DateTime.Today;
        }

        private void ButtonDateSetWeekends_OnClick(object sender, RoutedEventArgs e)
        {
            // 设置编辑中的作业的截止日期为周末
            var today = DateTime.Today;
            var delta = DayOfWeek.Saturday - today.DayOfWeek + 1;
            ViewModel.EditingHomework.DueTime = today + TimeSpan.FromDays(delta);
        }

        private void ButtonExpandingSwitcher_OnClick(object sender, RoutedEventArgs e)
        {
            // 切换窗口的展开和收缩状态
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
            // 当窗口失去焦点时，可以在这里添加逻辑
            //MainListView.SelectedIndex = -1;
        }

        private async void ButtonExport_OnClick(object sender, RoutedEventArgs e)
        {
            // 设置视图模型的 IsWorking 属性为 true，表示当前正在处理导出操作
            ViewModel.IsWorking = true;

            // 初始化一个文件保存对话框组件
            var dialog = new System.Windows.Forms.SaveFileDialog()
            {
                // 设置对话框中显示的文件类型过滤器，这里只允许保存 PNG 格式的图片
                Filter = "图片 (*.png)|*.png"
            };

            // 显示文件保存对话框，并检查用户是否点击了“保存”按钮
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                goto done;
            }

            // 调用 ExitEditingMode 方法退出编辑模式
            ExitEditingMode();

            // 等待一个任务调度周期，确保 UI 操作完成后再进行后续操作
            await Task.Yield();

            // 获取用户选择的文件保存路径
            var file = dialog.FileName;

            // 创建一个新的绘图视觉对象
            var visual = new DrawingVisual();
            // 从设置服务中获取当前的缩放比例
            var s = SettingsService.Settings.Scale;

            // 打开视觉对象的渲染上下文
            using (var context = visual.RenderOpen())
            {
                // 创建一个新的视觉画刷，用于将 MainListView 的视觉内容绘制到绘图面上
                var brush = new VisualBrush(MainListView)
                {
                    Stretch = Stretch.None  // 设置画刷的拉伸模式为 None，即不拉伸
                };

                // 从应用的资源中找到名为 MaterialDesignPaper 的画刷
                var bg = (System.Windows.Media.Brush)FindResource("MaterialDesignPaper");

                // 在渲染上下文中绘制背景
                context.DrawRectangle(bg, null, new Rect(0, 0, MainListView.ActualWidth * s, MainListView.ActualHeight * s));

                // 在渲染上下文中绘制 MainListView 的内容
                context.DrawRectangle(brush, null, new Rect(0, 0, MainListView.ActualWidth * s, MainListView.ActualHeight * s));
            }

            // 创建一个目标为位图的渲染对象，用于将视觉对象转换为位图
            var bitmap = new RenderTargetBitmap((int)(MainListView.ActualWidth * s), (int)(ActualHeight * s), 96d, 96d, PixelFormats.Default);
            bitmap.Render(visual);

            // 创建一个 PNG 位图编码器，用于将位图编码为 PNG 格式
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            // 尝试将编码后的 PNG 数据保存到文件中
            try
            {
                // 使用 FileStream 创建文件流，用于写入文件
                using (var stream = new FileStream(file, FileMode.Create, FileAccess.Write))
                {
                    // 将编码器中的数据写入文件流
                    encoder.Save(stream);

                    // 调用 ShowExportSuccessMessage 方法显示导出成功的提示信息
                    await ShowExportSuccessMessage(file);
                }
            }
            catch (Exception ex)
            {
                // 如果在导出过程中发生异常，将异常信息添加到 SnackbarMessageQueue 中显示
                ViewModel.SnackbarMessageQueue.Enqueue($"导出失败：{ex.Message}");
            }

        done:
            // 释放文件保存对话框所占用的资源
            dialog.Dispose();

            // 设置视图模型的 IsWorking 属性为 false，表示导出操作已完成
            ViewModel.IsWorking = false;
        }

        //一件导出到？盘
        private static int fileIndex = 0;
        private async void AutoExport(object sender, RoutedEventArgs e)
        {
            // 文件夹名称
            string folderName = "备份";

            // 获取当前应用程序的执行目录
            string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // 设置视图模型的 IsWorking 属性为 true，表示当前正在处理导出操作
            ViewModel.IsWorking = true;

            // 组合目录，并确保备份文件夹存在
            string folderPath = Path.Combine(currentDirectory, folderName);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // 调用 ExitEditingMode 方法退出编辑模式
            ExitEditingMode();

            // 等待一个任务调度周期，确保 UI 操作完成后再进行后续操作
            await Task.Yield();

            // 文件基本名称
            string baseFileName = "备份文件";
            // 文件扩展名
            string fileExtension = ".png";
            // 保留的最新文件数量
            const int maxFiles = 20;

            // 获取备份文件夹内所有以“备份文件”开头的文件
            var backupFiles = Directory.GetFiles(folderPath)
                                         .Where(f => Path.GetFileName(f).StartsWith(baseFileName))
                                         .Select(f => new FileInfo(f))
                                         .ToList();

            // 如果备份文件数量达到上限，则删除最旧的文件
            if (backupFiles.Count >= maxFiles)
            {
                // 获取最旧的文件路径
                string oldestFilePath = backupFiles.OrderBy(fi => fi.CreationTime).First().FullName;
                // 删除最旧的文件
                File.Delete(oldestFilePath);
            }

            // 确保fileIndex在1到maxFiles之间
            fileIndex = (fileIndex + 1) % (maxFiles + 1);
            if (fileIndex == 0) fileIndex = 1;

            // 生成新的文件名
            string newFileName = $"{baseFileName}{fileIndex}{fileExtension}";
            string filePath = Path.Combine(folderPath, newFileName);

            // 创建一个新的绘图视觉对象
            var visual = new DrawingVisual();
            // 从设置服务中获取当前的缩放比例
            var s = SettingsService.Settings.Scale;

            // 打开视觉对象的渲染上下文
            using (var context = visual.RenderOpen())
            {
                // 创建一个新的视觉画刷，用于将 MainListView 的视觉内容绘制到绘图面上
                var brush = new VisualBrush(MainListView)
                {
                    Stretch = Stretch.None  // 设置画刷的拉伸模式为 None，即不拉伸
                };

                // 从应用的资源中找到名为 MaterialDesignPaper 的画刷
                var bg = (System.Windows.Media.Brush)FindResource("MaterialDesignPaper");

                // 在渲染上下文中绘制背景
                context.DrawRectangle(bg, null, new Rect(0, 0, MainListView.ActualWidth * s, MainListView.ActualHeight * s));

                // 在渲染上下文中绘制 MainListView 的内容
                context.DrawRectangle(brush, null, new Rect(0, 0, MainListView.ActualWidth * s, MainListView.ActualHeight * s));
            }

            // 创建一个目标为位图的渲染对象，用于将视觉对象转换为位图
            var bitmap = new RenderTargetBitmap((int)(MainListView.ActualWidth * s), (int)(ActualHeight * s), 96d, 96d, PixelFormats.Default);
            bitmap.Render(visual);

            // 创建一个 PNG 位图编码器，用于将位图编码为 PNG 格式
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            // 尝试将编码后的 PNG 数据保存到文件中
            try
            {
                // 使用 FileStream 创建文件流，用于写入文件
                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    // 将编码器中的数据写入文件流
                    encoder.Save(stream);

                    // 调用 ShowExportSuccessMessage 方法显示导出成功的提示信息
                    //await ShowExportSuccessMessage(file);
                }
            }
            catch (Exception ex)
            {
                // 如果在导出过程中发生异常，将异常信息添加到 SnackbarMessageQueue 中显示
                ViewModel.SnackbarMessageQueue.Enqueue($"导出失败：{ex.Message}");
            }

        

            // 设置视图模型的 IsWorking 属性为 false，表示导出操作已完成
            ViewModel.IsWorking = false;
        }
        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Maximized)
            {
                // 如果窗口被最大化，尝试还原
                WindowState = WindowState.Normal;
            }
        }


        private async Task ShowExportSuccessMessage(string file)
        {
            // 将导出成功的提示信息添加到 SnackbarMessageQueue 中显示
            ViewModel.SnackbarMessageQueue.Enqueue($"成功地导出到：{file}", "查看", () =>
            {
                // 启动系统默认程序打开导出的文件
                Process.Start(new ProcessStartInfo()
                {
                    FileName = file,
                    UseShellExecute = true
                });
            });
        }

        private void DrawerHost_OnDrawerClosing(object? sender, DrawerClosingEventArgs e)
        {
            // 当抽屉关闭时，保存设置和用户配置文件
            SettingsService.SaveSettings();
            ProfileService.SaveProfile();
            AutoExport(null, null);
        }

        private void ButtonMore_Click(object sender, RoutedEventArgs e)
        {
            // 点击更多按钮，打开更多选项的弹出窗口
            PopupExAdvanced.IsOpen = true;
        }

        private void MainListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 当主列表视图的选择项发生变化时，可以在这里添加逻辑
            //ExitEditingMode(false);
        }

        public void OnTextBoxEnter()
        {
            // 如果在文本框中按下回车键，创建作业
            CreateHomework();
        }

        private void MenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            // 如果点击菜单项，关闭弹出窗口
            PopupExAdvanced.IsOpen = false;
        }

        private void MenuItemRecoverExpiredHomework_OnClick(object sender, RoutedEventArgs e)
        {
            // 如果点击恢复过期作业的菜单项，恢复过期作业
            RecoverExpiredHomework();
        }

        private void MenuItemBacktowork_OnClick(object sender , RoutedEventArgs e)
        {
            ViewModel.ExpiredHomeworks = ProfileService.CleanupOutdated();
            if (ViewModel.ExpiredHomeworks.Count > 0)
            {
                ViewModel.CanRecoverExpireHomework = true;
                // 如果有过期作业，显示提示信息，并提供恢复选项（误了）
            }
        }
        private void ButtonRestart_OnClick(object sender, RoutedEventArgs e)
        {
            // 点击重启按钮，重启应用程序
            AutoExport(null, null);
            App.ReleaseLock();
            System.Windows.Forms.Application.Restart();
            System.Windows.Application.Current.Shutdown();
        }
        private void MainWindow_OnDragOver(object sender, DragEventArgs e)
        {
            // 当拖动对象进入窗口时，可以在这里添加逻辑
            // 记录一条普通消息到 Sentry
        }

        private void MainWindow_OnDragEnter(object sender, DragEventArgs e)
        {
            // 当拖动对象进入窗口时，如果数据格式是文件，则处理
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;
            ViewModel.IsExpanded = false;
            ViewModel.IsUnlocked = false;
            SizeToContent = SizeToContent.Height;
            Width = Math.Min(ActualWidth, 350);
        }

        private async void RepositionEditingWindow()
        {
            // 重新定位编辑窗口
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
}