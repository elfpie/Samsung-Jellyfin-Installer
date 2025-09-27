 using Android.App;
 using Android.Content;
 using Android.Content.PM;
 using Android.OS;
 using Avalonia;
 using Avalonia.Android;
 using AvaloniaXplat.AndroidServices.Services;
 using AvaloniaXplat.Models;
 using AvaloniaXplat.Services;

namespace AvaloniaXplat.Android;

[Activity(
    Label = "AvaloniaXplat.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
 public class MainActivity : AvaloniaMainActivity<App>
 {
     private SamsungLoginBroadcastReceiver _samsungLoginReceiver;

     protected override void OnCreate(Bundle savedInstanceState)
     {
         // Set the Android browser service factory before the app initializes
         PlatformServices.BrowserServiceFactory = () => new AndroidBrowserService();

         // Register broadcast receiver for Samsung login callback
         _samsungLoginReceiver = new SamsungLoginBroadcastReceiver();
         var filter = new IntentFilter("SamsungLoginCallback");
          RegisterReceiver(_samsungLoginReceiver, filter, ReceiverFlags.NotExported);

         base.OnCreate(savedInstanceState);
     }

     protected override void OnDestroy()
     {
         base.OnDestroy();
         if (_samsungLoginReceiver != null)
         {
             UnregisterReceiver(_samsungLoginReceiver);
         }
     }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}