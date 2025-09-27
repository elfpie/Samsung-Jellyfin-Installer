 using System;

 namespace AvaloniaXplat.Services
 {
     public static class PlatformServices
     {
         public static Func<IBrowserService> BrowserServiceFactory { get; set; } = () => new AvaloniaXplat.DesktopServices.Services.DesktopBrowserService();
     }
 }