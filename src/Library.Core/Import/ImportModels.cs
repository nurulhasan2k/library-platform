using Library.Core.Contracts;
using Library.Core.Models;

namespace Library.Core.Import;

/// <summary>One raw line from a file or an external provider, before any checking. All values are text.</summary>
public sealed class ImportRecord
{
    public int RowNumber { get; set; }
    public string Type { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Branch { get; set; } = "";
    public string Category { get; set; } = "";
    public string Author { get; set; } = "";
    public string Artist { get; set; } = "";
    public string Year { get; set; } = "";
    public string Age { get; set; } = "";
}

/// <summary>A checked row: either ready to import (<see cref="Item"/> built) or with a list of problems.</summary>
public sealed class ImportRow
{
    public ImportRecord Record { get; init; } = new();
    public List<string> Errors { get; } = new();
    public Item? Item { get; set; }
    public bool IsValid => Errors.Count == 0 && Item != null;
}

public sealed class ImportPreview
{
    public string Source { get; init; } = "";
    public int DefaultBranchId { get; init; }
    public string? FileProblem { get; set; }
    public List<ImportRow> Rows { get; } = new();
    public int ValidCount => Rows.Count(r => r.IsValid);
    public int InvalidCount => Rows.Count - ValidCount;
}

/// <summary>An external catalogue the importer can pull from (feature 3.6). The built-in one is a mock.</summary>
public interface IExternalCatalogueProvider
{
    string Name { get; }
    string Description { get; }
    IReadOnlyList<ImportRecord> Fetch();
}
