using Library.Core.Contracts;
using Library.Core.Models;
using Library.Core.Services;

namespace Library.Tests;

public static class PatronAuthTests
{
    public static void Run()
    {
        T.Section("Patron sign-in (card number + PIN)");
        using var p = new TestPlatform();
        var auth = p.Get<IPatronAuthService>();

        T.Test("the right card and PIN signs in", () =>
        {
            var r = TestPlatform.Wait(auth.SignInAsync("LIB-1001", "1111"));
            T.True(r.Success);
            T.Equal("Ava Thompson", r.Patron!.FullName);
        });
        T.Test("card numbers are not case sensitive and spaces are ignored", () =>
            T.True(TestPlatform.Wait(auth.SignInAsync(" lib-1001 ", "1111")).Success));
        T.Test("a wrong PIN and an unknown card give the same message (no hint which was wrong)", () =>
        {
            var wrong = TestPlatform.Wait(auth.SignInAsync("LIB-1002", "0001")).Error!;
            var unknown = TestPlatform.Wait(auth.SignInAsync("LIB-9999", "0001")).Error!;
            T.True(wrong.StartsWith("Card number or PIN is not correct."));
            T.True(unknown.StartsWith("Card number or PIN is not correct."));
        });
        T.Test("three wrong PINs lock the card (limit set to 3 in the test configuration)", () =>
        {
            for (var i = 0; i < 2; i++) T.False(TestPlatform.Wait(auth.SignInAsync("LIB-1010", "9999")).LockedOut);
            var third = TestPlatform.Wait(auth.SignInAsync("LIB-1010", "9999"));
            T.True(third.LockedOut);
            T.True(third.LockedUntil is not null);
        });
        T.Test("a locked card refuses even the right PIN", () =>
        {
            var r = TestPlatform.Wait(auth.SignInAsync("LIB-1010", "0000"));
            T.False(r.Success);
            T.True(r.LockedOut);
        });
        T.Test("a successful sign-in resets the failure count", () =>
        {
            TestPlatform.Wait(auth.SignInAsync("LIB-1003", "0000"));
            TestPlatform.Wait(auth.SignInAsync("LIB-1003", "0000"));
            T.Equal(2, p.Patron("LIB-1003").FailedPinAttempts);
            T.True(TestPlatform.Wait(auth.SignInAsync("LIB-1003", "3333")).Success);
            T.Equal(0, p.Patron("LIB-1003").FailedPinAttempts);
        });
        T.Test("PINs are stored hashed, never as typed", () =>
        {
            var hash = p.Patron("LIB-1001").PinHash;
            T.False(hash.Contains("1111") && hash.Length < 10);
            T.True(hash.Split('.').Length == 3);
        });
    }
}
