using System;
using Android.App;
using Android.Content;
using Android.Net;
using AvaloniaXplat.Services;

namespace AvaloniaXplat.AndroidServices.Services
{
    public class AndroidBrowserService : IBrowserService
    {
        public void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("URL cannot be null or empty", nameof(url));

            try
            {
                // For Samsung login, use WebView instead of external browser
                if (url.Contains("account.samsung.com"))
                {
                    var intent = new Intent(Application.Context, typeof(SamsungLoginActivity));
                    intent.PutExtra("LoginUrl", url);
                    intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
                    Application.Context.StartActivity(intent);
                }
                else
                {
                    // Fallback to external browser for other URLs
                    var intent = new Intent(Intent.ActionView);
                    intent.SetData(global::Android.Net.Uri.Parse(url));
                    intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
                    Application.Context.StartActivity(intent);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to open URL: {url}", ex);
            }
        }
    }
}