using Library.Core;
using Library.Core.Configuration;
using Library.Core.Contracts;
using Library.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Library.Lending.Tests;

/// <summary>Builds the real Person B services with a fake (simulated) clock and the demo data.</summary>
public sealed class Harness
{
    public DateOnly Today { get; } = DateOnly.FromDateTime(DateTime.Today);
    public DemoLibraryStore Store { get; }
    public SimulatedClock Clock { get; }
    public NotificationService Notifications { get; }
    public ReservationService Reservations { get; }
    public StubInMemoryLendingService Lending { get; }

    public Harness()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLibraryOptions(new ConfigurationBuilder().Build());
        services.AddLibraryPersonB();
        services.AddLibraryInMemoryLendingStubs();
        var provider = services.BuildServiceProvider();

        Store = provider.GetRequiredService<DemoLibraryStore>();
        Clock = provider.GetRequiredService<SimulatedClock>();
        Notifications = provider.GetRequiredService<NotificationService>();
        Reservations = provider.GetRequiredService<ReservationService>();
        Lending = provider.GetRequiredService<StubInMemoryLendingService>();
        Clock.SetSimulatedToday(Today);
    }
}

// Demo data: item 101 Dune (borrowed by Dan), 102 Clean Code (Dan, due in 2 days), 103 Abbey Road (Erin, overdue 5 days),
// 104 Lego (available), 105 Hobbit (available), 106 Chess (damaged).
// Patrons: 1 Alice (Email), 2 Bob (Sms), 3 Carol (Both), 4 Dan, 5 Erin.
public class ReservationRuleTests
{
    [Fact]
    public async Task Hold_on_an_available_item_is_refused()
    {
        var h = new Harness();
        var result = await h.Reservations.PlaceAsync(104, 1, ReservationSource.Web);
        Assert.False(result.Success);
        Assert.Contains("available", result.Error);
    }

    [Fact]
    public async Task Hold_on_a_borrowed_item_works_but_a_duplicate_is_rejected()
    {
        var h = new Harness();
        Assert.True((await h.Reservations.PlaceAsync(101, 1, ReservationSource.Web)).Success);
        var again = await h.Reservations.PlaceAsync(101, 1, ReservationSource.Web);
        Assert.False(again.Success);
        Assert.Contains("already", again.Error);
    }

    [Fact]
    public async Task Hold_on_a_damaged_item_works()
    {
        var h = new Harness();
        Assert.True((await h.Reservations.PlaceAsync(106, 1, ReservationSource.Desk)).Success);
    }

    [Fact]
    public async Task Patron_cannot_hold_an_item_they_have_on_loan()
    {
        var h = new Harness();
        var result = await h.Reservations.PlaceAsync(101, 4, ReservationSource.Web); // Dan has Dune
        Assert.False(result.Success);
        Assert.Contains("on loan", result.Error);
    }

    [Fact]
    public async Task Hold_limit_is_enforced()
    {
        var h = new Harness();
        var limited = new ReservationService(h.Store, h.Clock, h.Notifications,
            Options.Create(new ReservationOptions { MaxActiveHoldsPerPatron = 1 }));

        Assert.True((await limited.PlaceAsync(101, 1, ReservationSource.Web)).Success);
        var second = await limited.PlaceAsync(102, 1, ReservationSource.Web);
        Assert.False(second.Success);
        Assert.Contains("limit", second.Error);
    }

    [Fact]
    public async Task Unknown_item_gives_a_friendly_message()
    {
        var h = new Harness();
        var result = await h.Reservations.PlaceAsync(99999, 1, ReservationSource.Web);
        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task Queue_is_in_order_and_positions_update_after_a_cancel()
    {
        var h = new Harness();
        var alice = await h.Reservations.PlaceAsync(101, 1, ReservationSource.Web);
        await h.Reservations.PlaceAsync(101, 2, ReservationSource.Web);
        await h.Reservations.PlaceAsync(101, 3, ReservationSource.Web);

        var before = await h.Reservations.GetQueueAsync(101);
        Assert.Equal(new[] { 1, 2, 3 }, before.Select(q => q.BorrowerId).ToArray());

        await h.Reservations.CancelAsync(alice.ReservationId!.Value, 1);

        var after = await h.Reservations.GetQueueAsync(101);
        Assert.Equal(new[] { 2, 3 }, after.Select(q => q.BorrowerId).ToArray());
        Assert.Equal(new[] { 1, 2 }, after.Select(q => q.Position).ToArray());
    }

    [Fact]
    public async Task Cancelling_someone_elses_hold_is_refused()
    {
        var h = new Harness();
        var alice = await h.Reservations.PlaceAsync(101, 1, ReservationSource.Web);
        var result = await h.Reservations.CancelAsync(alice.ReservationId!.Value, 2);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Returning_the_item_makes_the_first_patron_Ready_and_protects_the_item()
    {
        var h = new Harness();
        await h.Reservations.PlaceAsync(101, 1, ReservationSource.Web); // Alice
        await h.Reservations.PlaceAsync(101, 2, ReservationSource.Web); // Bob

        var returned = await h.Lending.ReturnAsync("LIB-0001", 1, false);
        Assert.True(returned.Success);

        var queue = await h.Reservations.GetQueueAsync(101);
        Assert.Equal(ReservationStatus.Ready, queue[0].Status);
        Assert.Equal(1, queue[0].BorrowerId);
        Assert.Equal(ReservationStatus.Waiting, queue[1].Status);

        Assert.Contains(h.Notifications.GetAll(), n => n.Type == NotificationType.ReservationReady && n.BorrowerId == 1);

        // Bob cannot borrow the held item. Alice can, and that fulfils her hold.
        var bob = await h.Lending.BorrowAsync("LIB-0001", 2, 1);
        Assert.False(bob.Success);
        Assert.Contains("held", bob.Error);

        var alice = await h.Lending.BorrowAsync("LIB-0001", 1, 1);
        Assert.True(alice.Success);
        var afterBorrow = await h.Reservations.GetQueueAsync(101);
        Assert.Single(afterBorrow);
        Assert.Equal(2, afterBorrow[0].BorrowerId);
    }

    [Fact]
    public async Task Ready_hold_expires_then_next_patron_is_promoted_and_notified()
    {
        var h = new Harness();
        await h.Reservations.PlaceAsync(101, 1, ReservationSource.Web);
        await h.Reservations.PlaceAsync(101, 2, ReservationSource.Web);
        await h.Lending.ReturnAsync("LIB-0001", 1, false); // Alice is Ready, expires in 3 days

        h.Clock.SetSimulatedToday(h.Today.AddDays(2));
        Assert.Equal(0, await h.Reservations.ExpireOverdueHoldsAsync(h.Clock.Today));

        h.Clock.SetSimulatedToday(h.Today.AddDays(3));
        Assert.Equal(1, await h.Reservations.ExpireOverdueHoldsAsync(h.Clock.Today));

        var queue = await h.Reservations.GetQueueAsync(101);
        Assert.Single(queue);
        Assert.Equal(2, queue[0].BorrowerId);
        Assert.Equal(ReservationStatus.Ready, queue[0].Status);

        var all = h.Notifications.GetAll();
        Assert.Contains(all, n => n.Type == NotificationType.ReservationExpired && n.BorrowerId == 1);
        Assert.Contains(all, n => n.Type == NotificationType.ReservationReady && n.BorrowerId == 2);
    }

    [Fact]
    public async Task Item_with_an_empty_queue_just_becomes_available()
    {
        var h = new Harness();
        var returned = await h.Lending.ReturnAsync("LIB-0002", 1, false);
        Assert.True(returned.Success);
        Assert.Empty(await h.Reservations.GetQueueAsync(102));
        Assert.True((await h.Lending.BorrowAsync("LIB-0002", 1, 1)).Success);
    }

    [Fact]
    public async Task Fresh_patron_has_no_holds()
    {
        var h = new Harness();
        Assert.Empty(h.Reservations.GetHoldsForBorrower(5));
        await Task.CompletedTask;
    }
}

public class NotificationRuleTests
{
    [Fact]
    public async Task Due_soon_is_sent_once_per_loan()
    {
        var h = new Harness();
        await h.Notifications.RunScheduledChecksAsync(h.Today);
        var second = await h.Notifications.RunScheduledChecksAsync(h.Today);

        Assert.Equal(0, second);
        Assert.Single(h.Notifications.GetAll(), n => n.Type == NotificationType.DueSoon && n.BorrowerId == 4);
    }

    [Fact]
    public async Task Fine_is_sent_at_most_once_per_loan_per_day()
    {
        var h = new Harness();
        await h.Notifications.RunScheduledChecksAsync(h.Today);
        await h.Notifications.RunScheduledChecksAsync(h.Today);
        Assert.Single(h.Notifications.GetAll(), n => n.Type == NotificationType.FineAccruing);

        await h.Notifications.RunScheduledChecksAsync(h.Today.AddDays(1));
        Assert.Equal(2, h.Notifications.GetAll().Count(n => n.Type == NotificationType.FineAccruing));
    }

    [Fact]
    public async Task Both_channels_create_two_records()
    {
        var h = new Harness();
        var borrow = await h.Lending.BorrowAsync("LIB-0004", 3, 1); // Carol prefers Both
        Assert.True(borrow.Success);

        var carol = h.Notifications.GetAll().Where(n => n.BorrowerId == 3 && n.Type == NotificationType.Borrowed).ToList();
        Assert.Equal(2, carol.Count);
        Assert.Contains(carol, n => n.Channel == NotificationChannel.Email);
        Assert.Contains(carol, n => n.Channel == NotificationChannel.Sms);
    }

    [Fact]
    public async Task Single_channel_patron_gets_one_record_in_the_preferred_channel()
    {
        var h = new Harness();
        await h.Lending.BorrowAsync("LIB-0005", 2, 1); // Bob prefers Sms
        var bob = h.Notifications.GetAll().Where(n => n.BorrowerId == 2).ToList();
        Assert.Single(bob);
        Assert.Equal(NotificationChannel.Sms, bob[0].Channel);
    }

    [Fact]
    public async Task Bodies_never_contain_a_pin_or_card_number()
    {
        var h = new Harness();
        await h.Lending.BorrowAsync("LIB-0004", 1, 1);
        await h.Notifications.RunScheduledChecksAsync(h.Today);

        foreach (var n in h.Notifications.GetAll())
        {
            Assert.DoesNotContain("1234", n.Body);
            Assert.DoesNotContain("1001", n.Body);
        }
    }
}
