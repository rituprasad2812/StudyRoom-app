using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudyRoom.Data;
using StudyRoom.Hubs;
using StudyRoom.Models;
using StudyRoom.Services;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Force HTTP for dev (single port)
builder.WebHost.UseUrls("http://localhost:5151");

// Logging so you see “Now listening…” and our custom message
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);

// EF + Identity
var cs = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db";
builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(cs));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddDefaultIdentity<ApplicationUser>(o => o.SignIn.RequireConfirmedAccount = false)
.AddEntityFrameworkStores<ApplicationDbContext>();

// MVC + SignalR + Services
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IRoomTimerManager, RoomTimerManager>();
builder.Services.AddSingleton<IRoomPresenceService, RoomPresenceService>();
builder.Services.AddMemoryCache();
builder.Services.AddSignalR(o => { o.EnableDetailedErrors = true; });
builder.Services.AddScoped<IBadgeService, BadgeService>();

var app = builder.Build();

// Pipeline (no HTTPS redirect while we’re HTTP-only)
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// app.UseHttpsRedirection(); // TEMP OFF (HTTP-only dev)
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Routes
app.MapControllerRoute(
name: "default",
pattern: "{controller=Rooms}/{action=Index}/{id?}");
app.MapControllerRoute(
name: "dashboard",
pattern: "dashboard/{action=Index}/{id?}",
defaults: new { controller = "Dashboard" });
app.MapRazorPages();

// Hubs
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<TimerHub>("/hubs/timer");
app.MapHub<TasksHub>("/hubs/tasks");
app.MapHub<PollsHub>("/hubs/polls");

// Migrate + seed badges
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    SeedData.EnsureBadges(db);
    SeedData.EnsureDisplayNames(db); 
}
catch (Exception ex)
{
    Console.WriteLine("DB init error: " + ex.Message);
}

Console.WriteLine("App starting on http://localhost:5151"); // explicit message
app.Run();
