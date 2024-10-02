using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace StickyHomeworks.Models;

public class Profile : ObservableRecipient
{
    public ObservableCollection<Homework> Homeworks { get; set; } = new();
}