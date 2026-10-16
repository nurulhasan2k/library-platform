using Microsoft.Extensions.Caching.Memory;

namespace Library.Core.Import;

public sealed class ImportPreviewStore(IMemoryCache cache)
{
    private readonly object gate = new();
    private static string Key(string token) => "LibraryImport:Preview:" + token;

    public void Save(PendingImport preview)
        => cache.Set(Key(preview.Token), preview, TimeSpan.FromMinutes(30));

    public PendingImport? Get(string token, int userId, DateTime now, bool consume = false)
    {
        if (token.Length != 64 || !token.All(Uri.IsHexDigit))
            return null;
        lock (gate)
        {
            if (!cache.TryGetValue<PendingImport>(Key(token), out var preview) || preview is null)
                return null;
            if (now >= preview.ExpiresAt)
            {
                cache.Remove(Key(token));
                return null;
            }
            if (preview.ByUserId != userId)
                return null;
            if (consume)
                cache.Remove(Key(token));
            return preview;
        }
    }
}
