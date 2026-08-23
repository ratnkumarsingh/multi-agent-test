using Microsoft.EntityFrameworkCore;
using PositiveNews.Core.Data;
using PositiveNews.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Same SQLite file PositiveNews.Cli writes — resolved via AppContext.BaseDirectory (not a
// relative "positivenews.db" path) so both processes land on the same physical file
// regardless of which one is running or its working directory.
var dbPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "positivenews.db");
builder.Services.AddDbContextFactory<PositiveNewsDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath};Default Timeout=10"));

var app = builder.Build();

// Apply any pending migrations so a fresh checkout doesn't crash on first browse if
// PositiveNews.Cli hasn't been run yet — idempotent if they're already applied.
using (var scope = app.Services.CreateScope())
{
    var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PositiveNewsDbContext>>();
    await using var db = await dbContextFactory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
