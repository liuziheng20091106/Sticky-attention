using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace StickyHomeworks.Controls;

/// <summary>
/// WindowMovingDemo.xaml 的交互逻辑
/// </summary>
public partial class WindowMovingDemo : UserControl
{
    public WindowMovingDemo()
    {
        InitializeComponent();
    }

    protected override async void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty)
        {
            var loop = (Storyboard)FindResource("Loop");
            if ((bool)e.NewValue)
            {
                loop.Remove();
                //loop.Seek(TimeSpan.Zero);
                BeginStoryboard(loop);
                //Debug.WriteLine("LOADED.");

            }
            else
            {
                loop.Remove();
                //loop.Seek(TimeSpan.Zero);
                //Debug.WriteLine("Unloaded.");
            }
        }
        base.OnPropertyChanged(e);
    }

    private void WindowMovingDemo_OnLoaded(object sender, RoutedEventArgs e)
    {
        BeginStoryboard((Storyboard)FindResource("Loop"));
    }

    private void WindowMovingDemo_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        var sb = (Storyboard)FindResource("Loop");
        if (IsVisible)
            sb.Begin(this);
        else
        {
            sb.Stop(this);
            sb.Remove(this);
        }
    }
}