namespace Library.Web.Models;

/// <summary>Data for the Previous/Next links under a list.</summary>
public sealed class PagerModel
{
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int Total { get; init; }
    public required Func<int, string> Url { get; init; }
}
