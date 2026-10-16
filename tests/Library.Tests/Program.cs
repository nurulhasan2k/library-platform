using Library.Tests;

Console.WriteLine("Library platform tests");

SeedTests.Run();
InventoryTests.Run();
LendingTests.Run();
TransferTests.Run();
ReservationAndNotificationTests.Run();
PatronAuthTests.Run();
CatalogueTests.Run();
ImportTests.Run();
ReportTests.Run();

return T.Finish();
