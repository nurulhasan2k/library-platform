using System.Text.Json.Serialization;
using Library.Core.Contracts;

namespace Library.Core.Models;

public enum UserRole { Admin, Reception, Manager }
public enum LoanChannel { Desk, Kiosk }

public sealed class Branch
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<OpeningHours> Hours { get; set; } = new();
    public List<Desk> Desks { get; set; } = new();

    public OpeningHours? HoursFor(DayOfWeek day) => Hours.FirstOrDefault(h => h.Day == day);

    public bool IsOpenAt(DateTime moment)
    {
        var h = HoursFor(moment.DayOfWeek);
        if (h is null || h.IsClosed) return false;
        var t = TimeOnly.FromDateTime(moment);
        return t >= h.Open && t < h.Close;
    }
}

public sealed class Desk
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public ItemType ItemType { get; set; }
}

public sealed class Borrower
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public string CardNumber { get; set; } = "";
    public string PinHash { get; set; } = "";
    public NotificationChannel PreferredChannel { get; set; } = NotificationChannel.Email;
    public int FailedPinAttempts { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }

    public IEnumerable<string> GetValidationErrors()
    {
        if (string.IsNullOrWhiteSpace(Name)) yield return "Name is required.";
        if (string.IsNullOrWhiteSpace(Phone) && string.IsNullOrWhiteSpace(Email))
            yield return "At least a phone number or an email address is required.";
        if (!string.IsNullOrWhiteSpace(Email) && !Email.Contains('@')) yield return "Email address doesn't look valid.";
        if (PreferredChannel is NotificationChannel.Email or NotificationChannel.Both && string.IsNullOrWhiteSpace(Email))
            yield return "An email address is needed for email notifications.";
        if (PreferredChannel is NotificationChannel.Sms or NotificationChannel.Both && string.IsNullOrWhiteSpace(Phone))
            yield return "A phone number is needed for SMS notifications.";
    }
}

public sealed class Loan
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public int BorrowerId { get; set; }
    /// <summary>Branch where the item was lent.</summary>
    public int BranchId { get; set; }
    public int? DeskId { get; set; }
    public LoanChannel Channel { get; set; } = LoanChannel.Desk;
    public DateOnly BorrowedOn { get; set; }
    public DateOnly DueOn { get; set; }
    public DateOnly? ReturnedOn { get; set; }
    public int? ReturnBranchId { get; set; }
    public bool ReturnedDamaged { get; set; }
    public decimal FineAmount { get; set; }
    public bool FinePaid { get; set; }
    public DateOnly? FinePaidOn { get; set; }

    [JsonIgnore] public bool IsActive => ReturnedOn is null;
    [JsonIgnore] public bool HasOutstandingFine => FineAmount > 0 && !FinePaid;
    public int DaysLate(DateOnly asOf) => Math.Max(0, asOf.DayNumber - DueOn.DayNumber);
}

public sealed class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }
    /// <summary>Branches this user may work at. Empty means all branches.</summary>
    public List<int> BranchIds { get; set; } = new();

    public bool MayUseBranch(int branchId) => BranchIds.Count == 0 || BranchIds.Contains(branchId);
}

public sealed class Transfer
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public int FromBranchId { get; set; }
    public int ToBranchId { get; set; }
    public TransferStatus Status { get; set; }
    public int RequestedByUserId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? Note { get; set; }
    [JsonIgnore] public bool IsActive => Status is TransferStatus.Requested or TransferStatus.InTransit;
}

public sealed class Reservation
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public int BorrowerId { get; set; }
    public ReservationStatus Status { get; set; }
    public ReservationSource Source { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    [JsonIgnore] public bool IsOpen => Status is ReservationStatus.Waiting or ReservationStatus.Ready;
}

public sealed class Notification
{
    public int Id { get; set; }
    public NotificationType Type { get; set; }
    public NotificationChannel Channel { get; set; }
    public int BorrowerId { get; set; }
    public string BorrowerName { get; set; } = "";
    public string Recipient { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int? BranchId { get; set; }
    public int? ItemId { get; set; }
    public int? LoanId { get; set; }
    public int? ReservationId { get; set; }
    public string DedupKey { get; set; } = "";
}

public sealed class ImportBatch
{
    public int Id { get; set; }
    public string Source { get; set; } = "";
    public int BranchId { get; set; }
    public string ImportedBy { get; set; } = "";
    public DateTime ImportedAt { get; set; }
    public int TotalRows { get; set; }
    public int ImportedRows { get; set; }
    public int SkippedRows { get; set; }
    public List<string> Problems { get; set; } = new();
}

public sealed class Settings
{
    /// <summary>Admin "simulated today" (decision 10). Null means use the real date.</summary>
    public DateOnly? SimulatedToday { get; set; }
}
