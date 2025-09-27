using Avalonia.Controls;
using AvaloniaXplat.ViewModels;

namespace AvaloniaXplat.Views;

public partial class JellyfinConfigView : UserControl
{
    public JellyfinConfigView(JellyfinConfigViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}