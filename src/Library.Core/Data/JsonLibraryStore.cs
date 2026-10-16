using System.Text.Json;
using System.Text.Json.Serialization;

namespace Library.Core.Data;

/// <summary>
/// Stores the library in a single JSON file. Chosen so the project builds and runs with no
/// database server and no third-party packages.
/// </summary>
/// <remarks>
/// The console app and the web portal can be running at the same time against the same file:
/// every call re-reads the file, writes go through a cross-process lock file, and the file is
/// replaced atomically, so a reader never sees a half-written file.
/// </remarks>
public sealed class JsonLibraryStore : ILibraryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;
    private readonly object _gate = new();

    public JsonLibraryStore(string path)
    {
        _path = Path.GetFullPath(path);
    }

    public string FilePath => _path;

    public bool IsEmpty
    {
        get
        {
            lock (_gate)
            {
                return !File.Exists(_path) || new FileInfo(_path).Length == 0 || Load().Branches.Count == 0;
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            using var fileLock = AcquireFileLock();
            if (File.Exists(_path)) File.Delete(_path);
        }
    }

    public T Read<T>(Func<LibraryData, T> query)
    {
        lock (_gate)
        {
            return query(Load());
        }
    }

    public T Write<T>(Func<LibraryData, T> change)
    {
        lock (_gate)
        {
            using var fileLock = AcquireFileLock();
            var data = Load();
            var result = change(data); // if this throws, nothing is saved
            Save(data);
            return result;
        }
    }

    private LibraryData Load()
    {
        if (!File.Exists(_path)) return new LibraryData();

        using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        return JsonSerializer.Deserialize<LibraryData>(stream, JsonOptions) ?? new LibraryData();
    }

    private void Save(LibraryData data)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var temp = _path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, data, JsonOptions);
        }

        File.Move(temp, _path, overwrite: true);
    }

    /// <summary>Serialises writers across threads and across processes (console app + web).</summary>
    private FileStream AcquireFileLock()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var lockPath = _path + ".lock";
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(25);
            }
        }
    }
}
