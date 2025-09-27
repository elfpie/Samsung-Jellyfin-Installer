using Android.Content;
using AvaloniaXplat.Models;
using AvaloniaXplat.Services;

namespace AvaloniaXplat.Android
{
    [BroadcastReceiver(Exported = false)]
    public class SamsungLoginBroadcastReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            var accessToken = intent.GetStringExtra("AccessToken");
            var tokenType = intent.GetStringExtra("TokenType");
            var userId = intent.GetStringExtra("UserId");

            if (!string.IsNullOrEmpty(accessToken))
            {
                var auth = new SamsungAuth
                {
                    AccessToken = accessToken,
                    TokenType = tokenType,
                    UserId = userId
                };

                // Find the SamsungLoginService instance and invoke the callback
                // This is a simplified approach; in a real app, you might use a service locator or static instance
                SamsungLoginService.HandleAndroidCallback(auth);
            }
        }
    }
}