using Avalonia.Controls;
using AvaloniaXplat.ViewModels;

namespace AvaloniaXplat.Views;

public partial class InstallingWindow : UserControl
{
    public InstallingWindowViewModel ViewModel { get; }

    public InstallingWindow()
    {
        InitializeComponent();

        ViewModel = new InstallingWindowViewModel();
        DataContext = ViewModel;
    }
}