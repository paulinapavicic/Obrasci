using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Obrasci.Data;
using Obrasci.Models;
using Obrasci.Services;
using Obrasci.Services.ImageProcessing;



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


builder.Services.AddScoped<IPhotoService, PhotoService>();
builder.Services.AddScoped<ILoggingService, LoggingService>();
//Singletons- for Image processing
builder.Services.AddSingleton<IImageProcessingStrategy, OriginalStrategy>();
builder.Services.AddSingleton<IImageProcessingStrategy, ResizeStrategy>();
builder.Services.AddSingleton<IImageProcessingStrategy, GrayscaleStrategy>();
builder.Services.AddScoped<IActionLogger, ActionLogger>();


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
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
