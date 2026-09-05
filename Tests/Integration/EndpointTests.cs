using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Tests.Integration
{
   
    public class EndpointTests : IClassFixture<TestWebAppFactory>
    {
        private readonly HttpClient _client;

        public EndpointTests(TestWebAppFactory factory)
        {
            _client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        [Fact]
        public async Task Home_index_returns_success()
        {
            var res = await _client.GetAsync("/");
            res.IsSuccessStatusCode.Should().BeTrue();
        }

        [Fact]
        public async Task Metrics_endpoint_returns_text_with_known_keys()
        {
            await _client.GetAsync("/");
            var res = await _client.GetAsync("/metrics");
            res.IsSuccessStatusCode.Should().BeTrue();
            var body = await res.Content.ReadAsStringAsync();
            body.Should().Contain("http_requests_total");
            body.Should().Contain("photos_per_minute_rate");
        }

        [Fact]
        public async Task Health_endpoint_returns_healthy_payload()
        {
            var res = await _client.GetAsync("/health");
            res.IsSuccessStatusCode.Should().BeTrue();
            var body = await res.Content.ReadAsStringAsync();
            body.Should().Contain("Healthy");
            body.Should().Contain("uptimeSeconds");
        }

        [Fact]
        public async Task Photos_index_lists_recent_photos()
        {
            var res = await _client.GetAsync("/Photos");
            res.IsSuccessStatusCode.Should().BeTrue();
        }

        [Fact]
        public async Task Unknown_route_returns_404()
        {
            var res = await _client.GetAsync("/this-route-does-not-exist");
            res.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        }
    }
}
