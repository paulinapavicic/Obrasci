using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Obrasci.Aspects;
using Obrasci.Data;
using Obrasci.Metrics;
using Obrasci.Middleware;
using Obrasci.Models;
using Obrasci.Services;
using Obrasci.Services;
using Obrasci.Services.ImageProcessing;
using Obrasci.Services.Storage;
using System.Text;

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

// Harden Identity application cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
});

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

// Global cookie policy (Secure + HttpOnly + SameSite)
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.HttpOnly = HttpOnlyPolicy.Always;
    options.Secure = CookieSecurePolicy.Always;
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
});

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
builder.Services.AddScoped<IPhotoSnapshotService, PhotoSnapshotService>();
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

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT key is missing.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("JWT issuer is missing.");

var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("JWT audience is missing.");

builder.Services.AddScoped<JwtService>();

builder.Services.AddAuthentication()
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.RequireHttpsMetadata = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)),

            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudience = jwtAudience,

            ValidateLifetime = true,
            RequireExpirationTime = true,

            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__Host-Obrasci-CSRF";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
var app = builder.Build();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (AntiforgeryValidationException)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        await context.Response.WriteAsJsonAsync(new
        {
            message = "CSRF validation failed. A valid X-CSRF-TOKEN header is required."
        });
    }
});

await IdentitySeed.SeedAsync(app.Services);

// Error handling + HSTS
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Centralized security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none';";

    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, private";
    context.Response.Headers["Pragma"] = "no-cache";
    context.Response.Headers["Expires"] = "0";

    await next();
});

// Cookie policy before anything writes cookies
app.UseCookiePolicy();

// Static files with X-Content-Type-Options for CSS/JS/etc.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    }
});

app.UseRouting();

app.UseMiddleware<RequestMetricsMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

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

    return Results.Text(
        string.Join("\n", lines),
        "text/plain");
});

app.Run();

// Needed by WebApplicationFactory in integration tests.
public partial class Program { }