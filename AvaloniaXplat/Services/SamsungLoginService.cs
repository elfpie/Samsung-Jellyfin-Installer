 using System;
 using System.Net;
 using System.Runtime.InteropServices;
 using System.Text.Json;
 using System.Threading.Tasks;
 using System.Web;
 using AvaloniaXplat.Models;
 using AvaloniaXplat.Services;
 using Microsoft.AspNetCore.Builder;
 using Microsoft.AspNetCore.Hosting;
 using Microsoft.AspNetCore.Http;

namespace AvaloniaXplat.Services
{
    public class SamsungLoginService
    {
        private IWebHost _callbackServer;
        private const string CallbackUrl = "http://localhost:4794/signin/callback";
        private const string StateValue = "accountcheckdogeneratedstatetext";

         public Action<SamsungAuth> CallbackReceived;

         public void HandleAndroidCallback(string accessToken, string tokenType, string userId)
         {
             var auth = new SamsungAuth
             {
                 AccessToken = accessToken,
                 TokenType = tokenType,
                 UserId = userId
             };
             CallbackReceived?.Invoke(auth);
         }

         public static void HandleAndroidCallback(SamsungAuth auth)
         {
             // This is a static method to handle the callback from the broadcast receiver
             // In a real implementation, you might need to use a service locator or static instance
             // For now, we'll assume there's a way to get the current instance
         }

         public static async Task<SamsungAuth> PerformSamsungLoginAsync(IBrowserService browserService)
         {
             string loginUrl =
                 $"https://account.samsung.com/accounts/be1dce529476c1a6d407c4c7578c31bd/signInGate?locale=&clientId=v285zxnl3h&redirect_uri={HttpUtility.UrlEncode(CallbackUrl)}&state={StateValue}&tokenType=TOKEN";

             // Open the system browser
             try
             {
                 browserService.OpenUrl(loginUrl);
             }
             catch (Exception ex)
             {
                 throw new InvalidOperationException("Failed to open system browser.", ex);
             }

             SamsungAuth authResult = null;
             var service = new SamsungLoginService();

              // Start the callback server for all platforms
              await service.StartCallbackServer();

              // Wait for CallbackReceived
              var tcs = new TaskCompletionSource<SamsungAuth>();
              service.CallbackReceived += auth => { tcs.SetResult(auth); };

              authResult = await tcs.Task;

              await service.StopCallbackServer();

             return authResult;
         }


        public async Task StartCallbackServer()
        {
            _callbackServer = new WebHostBuilder()
                .UseKestrel()
                .UseUrls("http://localhost:4794")
                .Configure(app =>
                {
                    app.Run(async context =>
                    {
                        if (context.Request.Path == "/signin/callback" && context.Request.Method == "POST")
                        {
                            var form = await context.Request.ReadFormAsync();
                            var state = form["state"];
                            var codeJson = form["code"];

                            if (!string.IsNullOrEmpty(codeJson))
                            {
                                try
                                {
                                     var auth = JsonSerializer.Deserialize<SamsungAuth>(codeJson);
                                     if (auth != null)
                                     {
                                         auth.State = state; // Inject state manually
                                         CallbackReceived?.Invoke(auth);
                                         context.Response.ContentType = "text/html; charset=utf-8";
                                         context.Response.StatusCode = (int)HttpStatusCode.OK;
                                         await context.Response.WriteAsync(
                                             "<html><body><h1>Login successful. You can close this window.</h1></body></html>");
                                         return;
                                     }
                                }
                                 catch (Exception ex)
                                 {
                                     context.Response.ContentType = "text/html; charset=utf-8";
                                     await context.Response.WriteAsync(
                                         $"<html><body><h1>Error: {ex.Message}</h1></body></html>");
                                 }
                            }

                             context.Response.ContentType = "text/html; charset=utf-8";
                             context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                             await context.Response.WriteAsync("<html><body><h1>Invalid login response.</h1></body></html>");
                        }
                        else
                        {
                            context.Response.ContentType = "text/html; charset=utf-8";
                            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                            await context.Response.WriteAsync("<html><body><h1>Not Found</h1></body></html>");
                        }
                    });
                })
                .Build();

            await _callbackServer.StartAsync();
        }

        public async Task StopCallbackServer()
        {
            if (_callbackServer != null)
            {
                await _callbackServer.StopAsync();
                _callbackServer.Dispose();
                _callbackServer = null;
            }
        }
    }
}