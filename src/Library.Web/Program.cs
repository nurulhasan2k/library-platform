using Library.Core;

var builder = WebApplication.CreateBuilder(args);

// Settings: appsettings.json -> appsettings.{Environment}.json -> user-secrets (Development) -> environment variables.
builder.Services.AddControllersWithViews();
builder.Services.AddLibraryOptions(builder.Configuration);
builder.Services.AddLibraryReportExports();
builder.Services.AddLibraryReportScreens();

// Real services are registered here as each person merges them (docs/ARCHITECTURE.md, "Replacing a stub").
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
