using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using System.Text.Json.Serialization;
using SystemeAideBasculement.Components;
using SystemeAideBasculement.Context;
using SystemeAideBasculement.Hubs;
using SystemeAideBasculement.Models;
using SystemeAideBasculement.Services;
using SystemeAideBasculement.Services.Security;
using SystemeAideBasculement.Shared;

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

    builder.Services.Configure<SabNotificationOptions>(
        builder.Configuration.GetSection("SabNotifications"));

    // Add services to the container.
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddCascadingAuthenticationState();

    // Razor Pages pour /Account/Login et /Account/Logout
    builder.Services.AddRazorPages();
    // Service LDAP (LDAPS)

    builder.Services.Configure<LdapOptions>(
        builder.Configuration.GetSection("Ldap"));

    builder.Services.AddAuthorizationCore();

#if MOKADLDAP && DEBUG
    builder.Services.AddSingleton<IAdLdapService, MockAdLdapService>();
#else
    builder.Services.AddSingleton<IAdLdapService, AdLdapService>();
#endif
    // Cookie Authentication
    builder.Services
        .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/Denied";
            options.ExpireTimeSpan = TimeSpan.FromHours(48);
            options.SlidingExpiration = true;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });

    // Authorization Policy (claim-based)
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("CanEdit", policy =>
        policy.RequireRole("CCSAB"));

    });

    builder.Services.AddScoped<AuthService>();


    builder.Services.AddScoped<SabAuthStateProvider>();
    builder.Services.AddScoped<AuthenticationStateProvider>(
        sp => sp.GetRequiredService<SabAuthStateProvider>());
    
    builder.Services.AddScoped<IUserContextService, UserContextService>();

/*    builder.Services.AddScoped(sp =>
    {
        var nav = sp.GetRequiredService<NavigationManager>();
        return new HttpClient { BaseAddress = new Uri(nav.BaseUri) };
    });*/

    builder.Services.AddScoped<HttpClient>(sp =>
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri("https://localhost:7205/");
        return client;
    });

    builder.Services
        .AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter()
            );
        });

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

    builder.Services.AddScoped<IAideMemoireRepository, DataAccessService>();

    var app = builder.Build();

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
    app.UseAntiforgery();

    // Endpoints (AFTER middleware)

    app.MapControllers(); // This enables routing for your API controllers
    app.MapHub<NotificationHub>("/notifications");

    // Endpoints Razor Pages (Login/Logout)
    app.MapRazorPages();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    // Exemple endpoint d’écriture protégé (IMPORTANT: enforcement serveur)
    app.MapPost("/api/page/save", () => Results.Ok(new { ok = true }))
     .RequireAuthorization("CanEdit"); // pas juste l’UI ?4-6bb978??3-955893?

    app.MapPost("/api/auth/login",
        (HttpContext http, AuthService auth, AuthRequest req)
            => auth.LoginAsync(http, req))
       .AllowAnonymous();

    // Logout endpoint — clears cookie
    app.MapPost("/api/auth/logout", async (HttpContext http) =>
    {
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Ok();
    }).RequireAuthorization();

    app.MapGet("/api/auth/logout", async (HttpContext http) =>
    {
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/");
    }).RequireAuthorization();

    // Endpoint pour récupérer les infos de l’utilisateur courant (affichage dans le header)
    app.MapGet("/api/me", (HttpContext http, IUserContextService svc) =>
    {
        var userInfo = svc.GetUserInfo(http.User);
        return userInfo is null
            ? Results.Unauthorized()
            : Results.Ok(userInfo);
    })
    .RequireAuthorization();

    // Startup tasks
    using (var scope = app.Services.CreateScope())
    {
        var cache = scope.ServiceProvider.GetRequiredService<SabStateCache>();
        await cache.LoadInitialStateAsync();
    }


    // Activer WAL Pour BD SQLite (améliore les performances et la concurrence, important pour SignalR)
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AideMemoireDbContext>();

        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    }


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