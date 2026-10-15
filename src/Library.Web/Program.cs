using Library.Core;
using Library.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Settings: appsettings.json -> appsettings.{Environment}.json -> user-secrets (Development) -> environment variables.
builder.Services.AddControllersWithViews();
builder.Services.AddLibraryOptions(builder.Configuration);

// Real services are registered here as each person merges them (docs/ARCHITECTURE.md, "Replacing a stub").
// Person B: simulated clock, notifications, reservations, scheduler, seeder. Stand-in lending and card/PIN sign-in
// are only used while A's real services are not registered (TryAdd), so A's real services always win.
builder.Services.AddLibraryPersonB();
builder.Services.AddLibraryInMemoryLendingStubs();
// AddLibraryStubs() fills in anything that does not have a real implementation yet.
builder.Services.AddLibraryStubs();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Lets integration tests start the app: WebApplicationFactory<Program>.
public partial class Program;
