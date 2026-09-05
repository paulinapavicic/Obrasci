using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Obrasci.Data;
using Testcontainers.PostgreSql;

namespace Tests.Integration
{
    public class TestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
          .WithImage("postgres:17")
          .WithDatabase("obrasci_test")
          .WithUsername("postgres")
          .WithPassword("postgres")
          .Build();

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();
        }

        public async Task DisposeAsync()
        {
            await _postgres.DisposeAsync();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");
            builder.UseSetting(
    WebHostDefaults.PreventHostingStartupKey,
    "true");

            builder.ConfigureAppConfiguration((context, configuration) =>
            {
                var testSettings = new Dictionary<string, string?>
                {
                    ["Jwt:Key"] =
                        "IntegrationTestJwtKey_ThisMustBeLongEnough_123456789",
                    ["Jwt:Issuer"] = "Obrasci.IntegrationTests",
                    ["Jwt:Audience"] = "Obrasci.IntegrationTests"
                };

                configuration.AddInMemoryCollection(testSettings);
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(ApplicationDbContext));
                services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseNpgsql(_postgres.GetConnectionString());
                });

                var sp = services.BuildServiceProvider();

                using var scope = sp.CreateScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();

                db.Database.EnsureCreated();
            });
        }
    }
}
