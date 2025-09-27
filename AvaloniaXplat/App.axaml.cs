using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Net.Http;
using Avalonia.Markup.Xaml;
using AvaloniaXplat.Extensions;
using AvaloniaXplat.Helpers;
using AvaloniaXplat.Services;
using AvaloniaXplat.ViewModels;
using AvaloniaXplat.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaXplat;

public partial class App : Application
{
    private IServiceProvider _serviceProvider;
    public static IServiceProvider Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public async override void OnFrameworkInitializationCompleted()
    {
        ConfigureServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = mainWindow;
            await viewModel.InitializeAsync();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();
            await viewModel.InitializeAsync();

            singleViewPlatform.MainView = new MainView()
            {
                DataContext = viewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices()
    {
        var services = new ServiceCollection();

        // Services
        services.AddSingleton<AppSettings>(AppSettings.Default);
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<INetworkService, NetworkService>();
        services.AddSingleton<ITizenCertificateService, TizenCertificateService>();
        services.AddSingleton<ITizenInstallerService, TizenInstallerService>(provider =>
        {
            var browserService = provider.GetRequiredService<IBrowserService>();
            return new TizenInstallerService(
                provider.GetRequiredService<HttpClient>(),
                provider.GetRequiredService<IDialogService>(),
                provider.GetRequiredService<AppSettings>(),
                provider.GetRequiredService<JellyfinHelper>(),
                provider.GetRequiredService<OperatingSystemHelper>(),
                provider.GetRequiredService<ProcessHelper>(),
                provider.GetRequiredService<FileHelper>(),
                browserService);
        });

        // Register platform-specific browser service
        services.AddSingleton<IBrowserService>(PlatformServices.BrowserServiceFactory());

        services.AddSingleton<SamsungLoginService>();
        services.AddSingleton<HttpClient>();

        services.AddSingleton<DeviceHelper>();
        services.AddSingleton<PackageHelper>();
        services.AddSingleton<JellyfinHelper>();
        services.AddSingleton<CertificateHelper>();
        services.AddSingleton<FileHelper>();
        services.AddSingleton<OperatingSystemHelper>();
        services.AddSingleton<ProcessHelper>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddTransient<InstallationCompleteViewModel>();
        services.AddTransient<InstallingWindowViewModel>();

        // JellyfinConfigViewModel requires JellyfinHelper
        services.AddTransient<JellyfinConfigViewModel>(provider =>
        {
            var helper = provider.GetRequiredService<JellyfinHelper>();
            var localization = provider.GetRequiredService<ILocalizationService>();
            return new JellyfinConfigViewModel(helper, localization);
        });

        // Views
        services.AddSingleton<MainWindow>(provider =>
        {
            return new MainWindow
            {
                DataContext = provider.GetRequiredService<MainWindowViewModel>()
            };
        });

        services.AddTransient<JellyfinConfigView>(provider =>
        {
            var vm = provider.GetRequiredService<JellyfinConfigViewModel>();
            return new JellyfinConfigView(vm);
        });

        services.AddTransient<InstallingWindow>(provider =>
        {
            var vm = provider.GetRequiredService<InstallingWindowViewModel>();
            return new InstallingWindow
            {
                DataContext = vm
            };
        });

        services.AddTransient<InstallationCompleteWindow>(provider =>
        {
            var vm = provider.GetRequiredService<InstallationCompleteViewModel>();
            return new InstallationCompleteWindow(vm);
        });

        // Build and assign service provider
        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;

        // Set localization service globally
        var localizationService = _serviceProvider.GetRequiredService<ILocalizationService>();
        LocalizationExtensions.SetLocalizationService(localizationService);
    }



    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}