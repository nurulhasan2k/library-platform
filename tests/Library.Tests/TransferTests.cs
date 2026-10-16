using Library.Core.Contracts;
using Library.Core.Models;
using Library.Core.Services;

namespace Library.Tests;

public static class TransferTests
{
    public static void Run()
    {
        T.Section("Inter-branch transfers");
        using var p = new TestPlatform();
        var transfers = p.Get<ITransferService>();
        var admin = p.User("admin").Id;
        var north = p.Branch("NORTH").Id;
        var central = p.Branch("CENTRAL").Id;
        var river = p.Branch("RIVER").Id;

        T.Test("request -> dispatch -> receive moves the item to the new branch", () =>
        {
            var req = TestPlatform.Wait(transfers.RequestAsync(p.Item("BK-032").Id, north, admin, "for a patron"));
            T.True(req.Success, req.Error ?? "");
            T.Equal(TransferStatus.Requested, req.Transfer!.Status);
            T.Equal(central, p.Item("BK-032").BranchId);
            var disp = TestPlatform.Wait(transfers.DispatchAsync(req.Transfer.Id, admin));
            T.Equal(TransferStatus.InTransit, disp.Transfer!.Status);
            T.Equal(central, p.Item("BK-032").BranchId, "still at the sending branch while in transit");
            var rec = TestPlatform.Wait(transfers.ReceiveAsync(req.Transfer.Id, admin));
            T.Equal(TransferStatus.Received, rec.Transfer!.Status);
            T.Equal(north, p.Item("BK-032").BranchId);
        });
        T.Test("while a transfer is active the item can't be borrowed", () =>
        {
            var r = TestPlatform.Wait(((ILendingService)p.Lending).BorrowAsync("BK-030", p.Patron("LIB-1001").Id, central));
            T.False(r.Success);
            T.True(r.Error!.Contains("in transit"), r.Error);
        });
        T.Test("inbound and outbound lists show only active transfers for a branch", () =>
        {
            var inboundCentral = TestPlatform.Wait(transfers.GetInboundAsync(central));
            T.Equal(1, inboundCentral.Count); // MU-012
            T.Equal("MU-012", inboundCentral[0].LibraryCode);
            var outboundCentral = TestPlatform.Wait(transfers.GetOutboundAsync(central));
            T.Equal(1, outboundCentral.Count); // BK-030
            T.Equal(0, TestPlatform.Wait(transfers.GetInboundAsync(north)).Count);
        });
        T.Test("the seeded in-transit item can be received; then it can be borrowed at the new branch", () =>
        {
            var t = p.Store.Read(d => d.Transfers.First(x => x.Status == TransferStatus.InTransit));
            T.True(TestPlatform.Wait(transfers.ReceiveAsync(t.Id, admin)).Success);
            T.Equal(central, p.Item("MU-012").BranchId);
            T.True(TestPlatform.Wait(((ILendingService)p.Lending).BorrowAsync("MU-012", p.Patron("LIB-1001").Id, central)).Success);
        });
        T.Test("only a requested transfer can be cancelled; cancelling frees the item", () =>
        {
            var t = p.Store.Read(d => d.Transfers.First(x => x.Status == TransferStatus.Requested));
            T.True(TestPlatform.Wait(transfers.CancelAsync(t.Id, admin)).Success);
            T.False(TestPlatform.Wait(transfers.IsInTransitAsync(t.ItemId)));
            T.False(TestPlatform.Wait(transfers.CancelAsync(t.Id, admin)).Success);
        });
        T.Test("dispatch and receive refuse the wrong state", () =>
        {
            var req = TestPlatform.Wait(transfers.RequestAsync(p.Item("BK-001").Id, river, admin, null));
            T.False(TestPlatform.Wait(transfers.ReceiveAsync(req.Transfer!.Id, admin)).Success, "can't receive before dispatch");
            TestPlatform.Wait(transfers.DispatchAsync(req.Transfer.Id, admin));
            T.False(TestPlatform.Wait(transfers.DispatchAsync(req.Transfer.Id, admin)).Success, "can't dispatch twice");
            T.False(TestPlatform.Wait(transfers.CancelAsync(req.Transfer.Id, admin)).Success, "can't cancel once dispatched");
        });
        T.Test("request rules: not to the same branch, not while borrowed, not twice, not for destroyed or held items", () =>
        {
            T.False(TestPlatform.Wait(transfers.RequestAsync(p.Item("BK-002").Id, central, admin, null)).Success);
            T.True(TestPlatform.Wait(transfers.RequestAsync(p.Item("BK-010").Id, north, admin, null)).Error!.Contains("out on loan"));
            T.True(TestPlatform.Wait(transfers.RequestAsync(p.Item("BK-001").Id, north, admin, null)).Error!.Contains("already has a transfer"));
            T.True(TestPlatform.Wait(transfers.RequestAsync(p.Item("TY-001").Id, north, admin, null)).Error!.Contains("destroyed"));
            T.True(TestPlatform.Wait(transfers.RequestAsync(p.Item("BK-011").Id, north, admin, null)).Error!.Contains("held for a patron"));
        });
    }
}
