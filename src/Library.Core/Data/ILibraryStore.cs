namespace Library.Core.Data;

/// <summary>
/// Where the platform's data lives. Services talk only to this interface, so the JSON file implementation can be
/// swapped for a database without touching the business rules (see docs/DECISIONS.md, decision 1).
/// <see cref="Write{T}"/> is the unit of work: the callback sees the latest data, may change several records, and
/// either all of its changes are saved or (if it throws) none are.
/// </summary>
public interface ILibraryStore
{
    T Read<T>(Func<LibraryData, T> query);
    T Write<T>(Func<LibraryData, T> change);

    /// <summary>True when the store has no data yet (a fresh install).</summary>
    bool IsEmpty { get; }

    /// <summary>Delete everything (used by --reset-db).</summary>
    void Clear();
}

public static class LibraryStoreExtensions
{
    public static void Write(this ILibraryStore store, Action<LibraryData> change) =>
        store.Write<object?>(data => { change(data); return null; });
}
