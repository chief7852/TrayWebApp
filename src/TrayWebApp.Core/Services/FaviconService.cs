using TrayWebApp.Core.Models;

namespace TrayWebApp.Core.Services;

/// <summary>
/// Downloads simple origin favicons for web app menu/list display.
/// </summary>
public static class FaviconService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    public static async Task<string?> RefreshAsync(WebAppItem app, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Uri.TryCreate(app.Url, UriKind.Absolute, out var appUri))
            {
                return null;
            }

            var faviconUri = new Uri(appUri.GetLeftPart(UriPartial.Authority) + "/favicon.ico");
            using var response = await HttpClient.GetAsync(faviconUri, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!contentType.Contains("image", StringComparison.OrdinalIgnoreCase) &&
                !faviconUri.AbsolutePath.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (bytes.Length == 0 || bytes.Length > 512 * 1024)
            {
                return null;
            }

            AppPaths.EnsureDirectories();
            var fileName = $"{SanitizeFileName(app.Id)}.ico";
            var path = Path.Combine(AppPaths.FaviconsDirectory, fileName);
            await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
            return path;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Failed to refresh favicon for {app.Name}: {ex.Message}");
            return null;
        }
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars);
    }
}
