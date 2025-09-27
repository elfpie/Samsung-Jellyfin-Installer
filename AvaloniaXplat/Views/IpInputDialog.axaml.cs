using System.Threading.Tasks;
using Avalonia.Controls;
using AvaloniaXplat.Services;
using AvaloniaXplat.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaXplat.Views;

public partial class IpInputDialog : Window
{
    public IpInputDialogViewModel ViewModel { get; }

    public IpInputDialog()
    {
        InitializeComponent();

        var loc = App.Services.GetRequiredService<ILocalizationService>();

        ViewModel = new IpInputDialogViewModel(loc, confirmed => Close());
        DataContext = ViewModel;
    }

    public async Task<string?> ShowDialogAsync(Window parent)
    {
        await ShowDialog(parent);
        return ViewModel.EnteredIp;
    }
}
