using System.Text.Json;
using Library.Core.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Library.Core.Import;

public sealed class MockImportProvider(IOptions<ImportOptions> options, IHostEnvironment environment) : IExternalCatalogueProvider
{
    public async Task<ExternalCatalogueResult> ReadAsync(CancellationToken ct = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.MockProviderPath))
            return new([], "Set Import:MockProviderPath to the simulated catalogue JSON file.");
        try
        {
            var path = Path.GetFullPath(settings.MockProviderPath, environment.ContentRootPath);
            await using var file = File.OpenRead(path);
            using var bytes = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = await file.ReadAsync(buffer, ct)) > 0)
            {
                if (bytes.Length + read > settings.MaxFileSizeMb * 1024L * 1024L)
                    return new([], "The simulated catalogue exceeds the import file size limit.");
                bytes.Write(buffer, 0, read);
            }
            var content = bytes.ToArray().AsMemory();
            if (content.Span.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) content = content[3..];
            using var document = JsonDocument.Parse(content, new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return new([], "The simulated catalogue must contain an items array of objects.");
            if (items.GetArrayLength() > settings.MaxRows)
                return new([], "The simulated catalogue exceeds the import row limit.");
            var rows = new List<IReadOnlyDictionary<string, string>>();
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    return new([], "Every simulated catalogue item must be an object.");
                var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in item.EnumerateObject())
                {
                    var column = CatalogueCsvValidator.Columns.FirstOrDefault(name => name.Equals(property.Name, StringComparison.OrdinalIgnoreCase));
                    if (column is null) continue;
                    var value = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString()!,
                        JsonValueKind.Number => property.Value.GetRawText(),
                        JsonValueKind.Null => "",
                        _ => null
                    };
                    if (value is null || !fields.TryAdd(column, value))
                        return new([], "Simulated catalogue fields must be unique text or numeric values, or null.");
                }
                foreach (var column in CatalogueCsvValidator.Columns) fields.TryAdd(column, "");
                rows.Add(fields);
            }
            return new(rows, null);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new([], "The simulated catalogue file was not found. Check Import:MockProviderPath.");
        }
        catch (JsonException)
        {
            return new([], "The simulated catalogue JSON is malformed.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new([], "The simulated catalogue file could not be read. Check its path and permissions.");
        }
        catch (ArgumentException)
        {
            return new([], "The simulated catalogue path is invalid. Check Import:MockProviderPath.");
        }
    }
}
