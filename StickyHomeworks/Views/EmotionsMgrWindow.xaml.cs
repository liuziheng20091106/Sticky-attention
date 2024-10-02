using StickyHomeworks.Core.Context;
using System.Windows;

namespace StickyHomeworks.Views;

/// <summary>
/// EmotionsMgrWindow.xaml 的交互逻辑
/// </summary>
public partial class EmotionsMgrWindow : Window
{
    public AppDbContext DbContext { get; set; }

    public EmotionsMgrWindow(AppDbContext dbContext)
    {
        InitializeComponent();
        DataContext = this;
        DbContext = dbContext;
    }
}