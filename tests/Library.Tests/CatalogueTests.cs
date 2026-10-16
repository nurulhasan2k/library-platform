using Library.Core.Contracts;
using Library.Core.Models;
using Library.Core.Services;

namespace Library.Tests;

public static class CatalogueTests
{
    public static void Run()
    {
        T.Section("Catalogue queries (public search and the API)");
        using var p = new TestPlatform();
        var cat = p.Get<ICatalogueQuery>();
        PagedResult<CatalogueItem> Search(CatalogueFilter f) => TestPlatform.Wait(cat.SearchAsync(f));

        T.Test("destroyed items never appear (43 of 45)", () => T.Equal(43, Search(new CatalogueFilter(PageSize: 100)).TotalCount));
        T.Test("a destroyed item can't be fetched by code", () => T.True(TestPlatform.Wait(cat.FindByCodeAsync("BK-040")) is null));
        T.Test("but the importer's duplicate check still knows about it", () => T.True(TestPlatform.Wait(cat.CodeExistsAsync("BK-040"))));
        T.Test("text search matches name, author, artist and genre, all words required", () =>
        {
            T.Equal(1, Search(new CatalogueFilter(Text: "herbert")).TotalCount);
            T.True(Search(new CatalogueFilter(Text: "miles davis")).TotalCount >= 2);
            T.Equal(0, Search(new CatalogueFilter(Text: "herbert beatles")).TotalCount);
        });
        T.Test("filters by type, category and branch combine", () =>
        {
            T.Equal(23, Search(new CatalogueFilter(Type: ItemType.Book, PageSize: 100)).TotalCount); // 24 books minus the destroyed atlas
            var rock = TestPlatform.Wait(cat.GetCategoriesAsync()).First(c => c.Name == "Rock");
            T.True(Search(new CatalogueFilter(CategoryId: rock.Id)).Items.All(i => i.CategoryName == "Rock"));
            T.True(Search(new CatalogueFilter(BranchId: p.Branch("RIVER").Id, PageSize: 100)).Items.All(i => i.BranchCode == "RIVER"));
        });
        T.Test("'available only' leaves out borrowed, damaged and in-transit items", () =>
        {
            var items = Search(new CatalogueFilter(AvailableOnly: true, PageSize: 100)).Items;
            T.False(items.Any(i => i.LibraryCode is "BK-010" or "MU-010" or "MU-012" or "BK-030"));
            T.True(items.Any(i => i.LibraryCode == "BK-001"));
        });
        T.Test("an in-transit item says so", () => T.True(TestPlatform.Wait(cat.FindByCodeAsync("MU-012"))!.InTransit));
        T.Test("results are paged", () =>
        {
            var first = Search(new CatalogueFilter(PageSize: 10, Page: 1));
            T.Equal(10, first.Items.Count);
            T.Equal(5, first.TotalPages);
            T.Equal(3, Search(new CatalogueFilter(PageSize: 10, Page: 5)).Items.Count);
        });
        T.Test("the item view carries no patron, loan or fine information", () =>
        {
            var props = typeof(CatalogueItem).GetProperties().Select(x => x.Name.ToLowerInvariant()).ToList();
            T.False(props.Any(n => n.Contains("borrow") || n.Contains("loan") || n.Contains("fine") || n.Contains("patron") || n.Contains("due")));
        });
        T.Test("categories list item counts without destroyed items; branches include hours", () =>
        {
            var history = TestPlatform.Wait(cat.GetCategoriesAsync()).First(c => c.Name == "History");
            T.Equal(4, history.ItemCount); // BK-005, BK-009, BK-016, BK-031 (the atlas is destroyed)
            var branches = TestPlatform.Wait(cat.GetBranchesAsync());
            T.Equal(3, branches.Count);
            T.Equal(7, branches[0].Hours.Count);
        });
    }
}
