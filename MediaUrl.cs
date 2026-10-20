using System;

internal static class MediaUrl
{
    internal static bool ValidUrl(string value)
    {
        Uri uri;
        if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || (uri.Scheme != "https" && uri.Scheme != "http")) return false;
        string host = uri.Host.ToLowerInvariant();
        if (host == "youtu.be") return uri.AbsolutePath.Trim('/').Length > 0;
        if (host == "youtube.com" || host == "www.youtube.com" || host == "m.youtube.com" || host == "music.youtube.com")
            return (uri.AbsolutePath == "/watch" && System.Text.RegularExpressions.Regex.IsMatch(uri.Query, @"[?&]v=[A-Za-z0-9_-]+")) || System.Text.RegularExpressions.Regex.IsMatch(uri.AbsolutePath, @"^/(shorts|live|embed)/[A-Za-z0-9_-]+/?$");
        if (host == "on.soundcloud.com" || host == "snd.sc") return uri.AbsolutePath.Trim('/').Length > 0;
        if (host != "soundcloud.com" && host != "www.soundcloud.com" && host != "m.soundcloud.com") return false;
        string[] parts = uri.AbsolutePath.Trim('/').Split('/');
        return parts.Length >= 2 && parts[0].Length > 0 && parts[1].Length > 0 && parts[1] != "sets" && parts[0] != "discover" && parts[0] != "search";
    }

}
