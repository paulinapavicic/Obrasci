using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Obrasci.Aspects;
using Obrasci.Data;
using Obrasci.Metrics;
using Obrasci.Middleware;
using Obrasci.Models;
using Obrasci.Services;
using Obrasci.Services.ImageProcessing;
using Obrasci.Services.Storage;



var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();


builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
    })
    .AddGitHub(options =>
    {
        options.ClientId = builder.Configuration["Authentication:GitHub:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"]!;
        options.Scope.Add("user:email");
    });

builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<IAppMetrics, AppMetrics>();

builder.Services.AddScoped<PackageLimitService>();
builder.Services.AddScoped<IPackageLimitService>(sp =>
{
    var inner = sp.GetRequiredService<PackageLimitService>();
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<IPackageLimitService>();
    var metrics = sp.GetRequiredService<IAppMetrics>();

    var withLogging = LoggingAspect<IPackageLimitService>.Create(inner, logger);
    var withPerf = PerformanceAspect<IPackageLimitService>.Create(withLogging, logger, metrics);
    return withPerf;
});

builder.Services.AddScoped<IPhotoStorage, LocalFileSystemPhotoStorage>();
builder.Services.AddScoped<ILoggingService, LoggingService>();

builder.Services.AddScoped<ActionLogger>();
builder.Services.AddScoped<IActionLogger>(sp =>
{
    var inner = sp.GetRequiredService<ActionLogger>();
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<IActionLogger>();
    var metrics = sp.GetRequiredService<IAppMetrics>();

    var withLogging = LoggingAspect<IActionLogger>.Create(inner, logger);
    var withPerf = PerformanceAspect<IActionLogger>.Create(withLogging, logger, metrics);
    return withPerf;
});


builder.Services.AddScoped<PhotoService>();
builder.Services.AddScoped<IPhotoService>(sp =>
{
    var inner = sp.GetRequiredService<PhotoService>();
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<IPhotoService>();
    var metrics = sp.GetRequiredService<IAppMetrics>();
    var withLogging = LoggingAspect<IPhotoService>.Create(inner, logger);
    var withPerf = PerformanceAspect<IPhotoService>.Create(withLogging, logger, metrics);
    return withPerf;
});

builder.Services.AddSingleton<IImageProcessingStrategy, OriginalStrategy>();
builder.Services.AddSingleton<IImageProcessingStrategy, ResizeStrategy>();
builder.Services.AddSingleton<IImageProcessingStrategy, GrayscaleStrategy>();

builder.Services.AddHealthChecks();
var app = builder.Build();

await IdentitySeed.SeedAsync(app.Services);


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseMiddleware<RequestMetricsMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapGet("/health", () =>
{
    var payload = new
    {
        status = "Healthy",
        uptimeSeconds = Environment.TickCount64 / 1000
    };

    return Results.Json(payload);
});

app.MapGet("/metrics", (IAppMetrics metrics) =>
{
    var lines = metrics.Snapshot()
        .Select(kvp => $"{kvp.Key} {kvp.Value}");

    return Results.Text(string.Join("\n", lines), "text/plain");
});

app.Run();

// Needed by WebApplicationFactory in integration tests.
public partial class Program { }
