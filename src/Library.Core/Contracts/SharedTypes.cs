namespace Library.Core.Contracts;

// Shared vocabulary used by every service in the platform.
// These types are part of the FROZEN contract (docs/ARCHITECTURE.md). Change them only by agreement.

/// <summary>The kind of catalogue item. Matches the Assignment 2 Book / Music / Toy subclasses.</summary>
public enum ItemType
{
    Book,
    Music,
    Toy
}

/// <summary>Item status from the brief: available, borrowed, damaged, destroy.</summary>
public enum ItemStatus
{
    Available,
    Borrowed,
    Damaged,
    Destroy
}

/// <summary>How a patron wants to be contacted. "Both" produces two notification records.</summary>
public enum NotificationChannel
{
    Email,
    Sms,
    Both
}

/// <summary>A page of results for list screens and the public API.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Opening hours for one weekday. Open and Close are null when the branch is closed that day.</summary>
public sealed record OpeningHours(DayOfWeek Day, TimeOnly? Open, TimeOnly? Close)
{
    public bool IsClosed => Open is null || Close is null;
}
