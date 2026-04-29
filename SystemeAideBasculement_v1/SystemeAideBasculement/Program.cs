using NLog;
using NLog.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SystemeAideBasculement.Components;
using SystemeAideBasculement.Context;
using SystemeAideBasculement.Hubs;
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

    builder.Services.AddControllers();

    builder.Services.AddSignalR();

    builder.Services.AddSingleton<NotificationService>();

    builder.Services.AddSingleton<SabStateCache>();

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
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

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