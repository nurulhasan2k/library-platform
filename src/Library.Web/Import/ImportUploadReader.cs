namespace Library.Web.Import;

internal static class ImportUploadReader
{
    public static async Task<byte[]?> ReadAsync(IFormFile file, long maximumBytes, CancellationToken ct)
    {
        await using var input = file.OpenReadStream();
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            if (output.Length + read > maximumBytes) return null;
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        return output.ToArray();
    }
}
