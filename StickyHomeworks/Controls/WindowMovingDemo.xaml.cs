using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace StickyHomeworks.Controls
{
    /// <summary>
    /// WindowMovingDemo.xaml 的交互逻辑
    /// </summary>
    public partial class WindowMovingDemo : UserControl
    {
        public WindowMovingDemo()
        {
            InitializeComponent();
            IsVisibleChanged += WindowMovingDemo_OnIsVisibleChanged;
        }

        private void WindowMovingDemo_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var storyboard = (Storyboard)FindResource("Loop");

            if ((bool)e.NewValue)
            {
                storyboard.Begin(this, true); // 第二个参数 true 使动画以填充模式播放。
            }
            else
            {
                storyboard.Stop(this);
            }
        }

        private void WindowMovingDemo_OnLoaded(object sender, RoutedEventArgs e)
        {
            var storyboard = (Storyboard)FindResource("Loop");
            storyboard.Begin(this, true);
        }
    }
}
