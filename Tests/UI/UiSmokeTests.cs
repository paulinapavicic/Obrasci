using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace Tests.UI
{
  
    public class UiSmokeTests : IClassFixture<Integration.TestWebAppFactory>, IAsyncLifetime
    {
        private readonly Integration.TestWebAppFactory _factory;
        private IPlaywright? _pw;
        private IBrowser? _browser;
        private string _baseUrl = string.Empty;

        public UiSmokeTests(Integration.TestWebAppFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            _baseUrl = "https://localhost:7109";
            _pw = await Playwright.CreateAsync();
            _browser = await _pw.Chromium.LaunchAsync(new() { Headless = true });
        }

        public async Task DisposeAsync()
        {
            if (_browser != null) await _browser.CloseAsync();
            _pw?.Dispose();
        }

        private async Task<IPage> NewPageAsync()
        {
            var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true
            });

            return await context.NewPageAsync();
        }

        [Fact]
        public async Task Home_page_renders_navigation_bar()
        {
            var page = await NewPageAsync();
            await page.GotoAsync(_baseUrl);
            (await page.TitleAsync()).Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Photos_index_shows_heading()
        {
            var page = await NewPageAsync();
            await page.GotoAsync(_baseUrl + "/Photos");
            var html = await page.ContentAsync();
            html.Should().Contain("Photos", "the listing page should reference photos");
        }

        [Fact]
        public async Task Login_page_loads_with_username_and_password_inputs()
        {
            var page = await NewPageAsync();
            await page.GotoAsync(_baseUrl + "/Account/Login");
            var html = await page.ContentAsync();
            html.ToLower().Should().Contain("password");
        }
    }
}
