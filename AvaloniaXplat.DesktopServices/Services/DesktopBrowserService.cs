using System;
using AvaloniaXplat.Services;

namespace AvaloniaXplat.DesktopServices.Services
{
    public class DesktopBrowserService : IBrowserService
    {
        public void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentException("URL cannot be null or empty", nameof(url));

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to open URL in browser: {url}", ex);
            }
        }
    }
}