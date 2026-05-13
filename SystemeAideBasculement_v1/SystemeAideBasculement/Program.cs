using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using System.Text.Json.Serialization;
using SystemeAideBasculement.Components;
using SystemeAideBasculement.Context;
using SystemeAideBasculement.Hubs;
using SystemeAideBasculement.Models;
using SystemeAideBasculement.Services;

var logger = LogManager
    .Setup()
    .LoadConfigurationFromAppSettings() // picks up NLog.config if present
    .GetCurrentClassLogger();

try
{

    var builder = WebApplication.CreateBuilder(args);

    // Replace default providers with NLog
    builder.Logging.ClearProviders();
    builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
    builder.Host.UseNLog(); // NLog.Web.AspNetCore extension

    // Add services to the container.
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    // Razor Pages pour /Account/Login et /Account/Logout
    builder.Services.AddRazorPages();
    // Service LDAP (LDAPS)
    builder.Services.AddSingleton<AdLdapService>();
    // Cookie Authentication
    builder.Services
        .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/Denied";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });

    // Authorization Policy (claim-based)
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("CanEdit", policy =>
        policy.RequireClaim("group", "CCSAB"));

    });


    builder.Services
        .AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter()
            );
        });

    builder.Services.Configure<SabNotificationOptions>(
        builder.Configuration.GetSection("SabNotifications"));

    builder.Services.AddSignalR();

    builder.Services.AddSingleton<SabStateCache>();

    builder.Services.AddSingleton<INotificationQueue, NotificationQueue>();

    builder.Services.AddHostedService<NotificationWorker>();

    builder.Services.AddSingleton<NotificationService>();

    builder.Services.AddSingleton<JsonSchemaProvider>(sp =>
    {
        var env = sp.GetRequiredService<IWebHostEnvironment>();
        return new JsonSchemaProvider(env.WebRootPath);
    });

    //builder.Services.AddSingleton(new HttpClient
    //{
    //    BaseAddress = new Uri("https://localhost:5163/") // Remplace par l’URL réelle de ton application
    //});

    builder.Services.AddDbContext<AideMemoireDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("AideMemoireDb")));

    var app = builder.Build();

    app.MapControllers(); // This enables routing for your API controllers

    app.MapHub<NotificationHub>("/notifications");

    using (var scope = app.Services.CreateScope())
    {
        var cache = scope.ServiceProvider.GetRequiredService<SabStateCache>();
        await cache.LoadInitialStateAsync();
    }


    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    app.MapStaticAssets();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
    // Endpoints Razor Pages (Login/Logout)
    app.MapRazorPages();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    // Exemple endpoint d’écriture protégé (IMPORTANT: enforcement serveur)
    app.MapPost("/api/page/save", () => Results.Ok(new { ok = true }))

     .RequireAuthorization("CanEdit"); // pas juste l’UI ?4-6bb978??3-955893?

    app.UseAntiforgery();

    app.Run();
}
catch (Exception ex)
{
    // NLog: catch setup errors
    logger.Error(ex, "Stopped program because of exception");
    throw;
}
finally
{
    // Ensure to flush and stop internal timers/threads before application-exit (Avoid segmentation fault on Linux)
    LogManager.Shutdown();
}