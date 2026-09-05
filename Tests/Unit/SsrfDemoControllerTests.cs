using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Obrasci.Controllers;
using System.Reflection;
using Xunit;

namespace Tests.Unit
{
    public class SsrfDemoControllerTests
    {
        private readonly SsrfDemoController _controller = new();

        [Fact]
        public void Controller_has_expected_api_route()
        {
        
            var route = typeof(SsrfDemoController)
                .GetCustomAttribute<RouteAttribute>();

            route.Should().NotBeNull();
            route!.Template.Should().Be("api/ssrf-demo");
        }

        [Fact]
        public void ValidateUrl_has_expected_post_route()
        {
     
            var method = typeof(SsrfDemoController)
                .GetMethod(nameof(SsrfDemoController.ValidateUrl));

            var route = method!
                .GetCustomAttribute<HttpPostAttribute>();

     
            route.Should().NotBeNull();
            route!.Template.Should().Be("validate-url");
        }

        [Fact]
        public void ValidateUrl_binds_request_from_body()
        {
       
            var parameter = typeof(SsrfDemoController)
                .GetMethod(nameof(SsrfDemoController.ValidateUrl))!
                .GetParameters()
                .Single();

            var attribute = parameter
                .GetCustomAttribute<FromBodyAttribute>();

         
            attribute.Should().NotBeNull();
        }

        [Theory]
        [InlineData("https://example.com")]
        [InlineData("https://example.com/image.jpg")]
        [InlineData("https://www.example.com/path?size=large")]
        public void ValidateUrl_when_public_https_url_is_valid_returns_ok(
            string url)
        {
           
            var result = _controller.ValidateUrl(new UrlRequest
            {
                Url = url
            });

        
            var ok = result.Should()
                .BeOfType<OkObjectResult>()
                .Subject;

            ok.StatusCode.Should()
                .Be(StatusCodes.Status200OK);

            GetProperty(ok.Value!, "message")
                .Should()
                .Be("URL passed SSRF validation.");

            GetProperty(ok.Value!, "url")
                .Should()
                .Be(url);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not a URL")]
        [InlineData("example.com")]
        [InlineData("/images/photo.jpg")]
        public void ValidateUrl_when_url_is_missing_or_invalid_returns_bad_request(
            string? url)
        {
            
            var result = _controller.ValidateUrl(new UrlRequest
            {
                Url = url
            });

            
            AssertRejected(result);
        }

        [Theory]
        [InlineData("http://example.com")]
        [InlineData("ftp://example.com/file.jpg")]
        [InlineData("file:///etc/passwd")]
        [InlineData("gopher://example.com")]
        public void ValidateUrl_when_url_does_not_use_https_returns_bad_request(
            string url)
        {
            
            var result = _controller.ValidateUrl(new UrlRequest
            {
                Url = url
            });

           
            AssertRejected(result);
        }

        [Theory]
        [InlineData("https://localhost")]
        [InlineData("https://localhost:5001/secret")]
        [InlineData("https://127.0.0.1")]
        [InlineData("https://127.0.0.1:8080/admin")]
        [InlineData("https://[::1]/")]
        public void ValidateUrl_when_url_targets_loopback_returns_bad_request(
            string url)
        {
            
            var result = _controller.ValidateUrl(new UrlRequest
            {
                Url = url
            });

            
            AssertRejected(result);
        }

        [Theory]
        [InlineData("https://10.0.0.1")]
        [InlineData("https://172.16.0.1")]
        [InlineData("https://192.168.1.1")]
        [InlineData("https://169.254.169.254/latest/meta-data")]
        public void ValidateUrl_when_url_targets_private_or_link_local_address_returns_bad_request(
            string url)
        {
            
            var result = _controller.ValidateUrl(new UrlRequest
            {
                Url = url
            });

            
            AssertRejected(result);
        }

        [Theory]
        [InlineData("https://user:password@example.com")]
        [InlineData("https://example.com@127.0.0.1")]
        [InlineData("https://user@example.com")]
        public void ValidateUrl_when_url_contains_user_info_returns_bad_request(
            string url)
        {
            
            var result = _controller.ValidateUrl(new UrlRequest
            {
                Url = url
            });

           
            AssertRejected(result);
        }

        private static void AssertRejected(IActionResult result)
        {
            var badRequest = result.Should()
                .BeOfType<BadRequestObjectResult>()
                .Subject;

            badRequest.StatusCode.Should()
                .Be(StatusCodes.Status400BadRequest);

            GetProperty(badRequest.Value!, "message")
                .Should()
                .Be("URL rejected by SSRF protection.");

            GetProperty(badRequest.Value!, "reason")
                .Should()
                .BeOfType<string>()
                .Which
                .Should()
                .NotBeNullOrWhiteSpace();
        }

        private static object? GetProperty(
            object value,
            string propertyName)
        {
            return value.GetType()
                .GetProperty(
                    propertyName,
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.IgnoreCase)
                ?.GetValue(value);
        }
    }
}