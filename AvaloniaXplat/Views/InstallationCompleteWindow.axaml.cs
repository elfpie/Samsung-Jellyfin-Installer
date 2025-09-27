using Avalonia.Controls;
using AvaloniaXplat.ViewModels;

namespace AvaloniaXplat.Views;

public partial class InstallationCompleteWindow : UserControl
{
    public InstallationCompleteWindow(InstallationCompleteViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}