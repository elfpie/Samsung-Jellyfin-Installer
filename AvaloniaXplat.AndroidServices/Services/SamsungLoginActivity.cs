using System;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Webkit;
using Android.Widget;
using AvaloniaXplat.Models;
using AvaloniaXplat.Services;

namespace AvaloniaXplat.AndroidServices.Services
{
    [Activity(Label = "Samsung Login")]
    public class SamsungLoginActivity : Activity
    {
        private WebView _webView;
        private const string CallbackUrl = "http://localhost:4794/signin/callback";
        private SamsungWebViewClient _webViewClient;
        private SamsungLoginService _loginService;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Set up the layout
            _webView = new WebView(this);
            _webView.Settings.JavaScriptEnabled = true;
            _webView.Settings.DomStorageEnabled = true;
            _webViewClient = new SamsungWebViewClient(this);
            _webView.SetWebViewClient(_webViewClient);

            SetContentView(_webView);

            // Start the callback server
            _loginService = new SamsungLoginService();
            Task.Run(async () => await _loginService.StartCallbackServer());
            _loginService.CallbackReceived += auth =>
            {
                // Broadcast the auth data back
                var intent = new Intent("SamsungLoginCallback");
                intent.PutExtra("AccessToken", auth.AccessToken);
                intent.PutExtra("TokenType", auth.TokenType);
                intent.PutExtra("UserId", auth.UserId);
                Application.Context.SendBroadcast(intent);
                
                 // Close the activity immediately after receiving the callback
                 RunOnUiThread(() => Finish());
            };

            // Get the login URL from the intent
            var loginUrl = Intent.GetStringExtra("LoginUrl");
            if (!string.IsNullOrEmpty(loginUrl))
            {
                _webView.LoadUrl(loginUrl);
            }
        }

        private class SamsungWebViewClient : WebViewClient
        {
            private readonly SamsungLoginActivity _activity;

            public SamsungWebViewClient(SamsungLoginActivity activity)
            {
                _activity = activity;
            }

             public override bool ShouldOverrideUrlLoading(WebView view, IWebResourceRequest request)
             {
                 // Do not intercept the callback URL; let the WebView load it so the server can handle the POST
                 return base.ShouldOverrideUrlLoading(view, request);
             }




        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            // Stop the callback server
            if (_loginService != null)
            {
                Task.Run(async () => await _loginService.StopCallbackServer());
            }
        }
    }
}