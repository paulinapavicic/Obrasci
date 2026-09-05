using FluentAssertions;
using Obrasci.Services;
using Xunit;

namespace Tests.Unit
{
    public class SsrfUrlValidatorTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not a URL")]
        [InlineData("/relative/path")]
        [InlineData("example.com/image.jpg")]
        public void TryValidatePublicHttpsUrl_with_invalid_or_relative_url_returns_false(
            string? input)
        {
          
            var valid = SsrfUrlValidator.TryValidatePublicHttpsUrl(
                input,
                out var error);

      
            valid.Should().BeFalse();
            error.Should().Be("A valid absolute URL is required.");
        }

        [Theory]
        [InlineData("http://example.com/image.jpg")]
        [InlineData("ftp://example.com/file.jpg")]
        [InlineData("file:///C:/Windows/win.ini")]
        public void TryValidatePublicHttpsUrl_with_non_https_scheme_returns_false(
            string input)
        {
            
            var valid = SsrfUrlValidator.TryValidatePublicHttpsUrl(
                input,
                out var error);

         
            valid.Should().BeFalse();
            error.Should().Be("Only HTTPS URLs are allowed.");
        }

        [Theory]
        [InlineData("https://user@example.com/image.jpg")]
        [InlineData("https://user:password@example.com/image.jpg")]
        public void TryValidatePublicHttpsUrl_with_user_info_returns_false(
            string input)
        {
           
            var valid = SsrfUrlValidator.TryValidatePublicHttpsUrl(
                input,
                out var error);

           
            valid.Should().BeFalse();
            error.Should()
                .Be("URLs containing user information are not allowed.");
        }

        [Theory]
        [InlineData("https://localhost/image.jpg")]
        [InlineData("https://LOCALHOST/image.jpg")]
        [InlineData("https://127.0.0.1/image.jpg")]
        [InlineData("https://127.12.34.56/image.jpg")]
        [InlineData("https://[::1]/image.jpg")]
        public void TryValidatePublicHttpsUrl_with_loopback_or_localhost_returns_false(
            string input)
        {
           
            var valid = SsrfUrlValidator.TryValidatePublicHttpsUrl(
                input,
                out var error);

          
            valid.Should().BeFalse();
            error.Should()
                .Be("Loopback and localhost URLs are not allowed.");
        }

        [Theory]
        [InlineData("https://10.0.0.1/image.jpg")]
        [InlineData("https://10.255.255.255/image.jpg")]
        [InlineData("https://172.16.0.1/image.jpg")]
        [InlineData("https://172.31.255.255/image.jpg")]
        [InlineData("https://192.168.0.1/image.jpg")]
        [InlineData("https://192.168.255.255/image.jpg")]
        [InlineData("https://169.254.169.254/latest/meta-data")]
        public void TryValidatePublicHttpsUrl_with_private_or_link_local_ipv4_returns_false(
            string input)
        {
          
            var valid = SsrfUrlValidator.TryValidatePublicHttpsUrl(
                input,
                out var error);

           
            valid.Should().BeFalse();
            error.Should()
                .Be("Private, loopback, and link-local IP addresses are not allowed.");
        }

        [Theory]
        [InlineData("https://[fe80::1]/image.jpg")]
        [InlineData("https://[ff02::1]/image.jpg")]
        public void TryValidatePublicHttpsUrl_with_disallowed_ipv6_returns_false(
            string input)
        {
            
            var valid = SsrfUrlValidator.TryValidatePublicHttpsUrl(
                input,
                out var error);

            valid.Should().BeFalse();
            error.Should()
                .Be("Private, loopback, and link-local IP addresses are not allowed.");
        }

        [Theory]
        [InlineData("https://8.8.8.8/image.jpg")]
        [InlineData("https://1.1.1.1/path/to/image.jpg")]
        [InlineData("https://example.com/image.jpg")]
        [InlineData("https://subdomain.example.com/path?size=large")]
        public void TryValidatePublicHttpsUrl_with_public_https_url_returns_true(
            string input)
        {
            
            var valid = SsrfUrlValidator.TryValidatePublicHttpsUrl(
                input,
                out var error);

           
            valid.Should().BeTrue();
            error.Should().BeEmpty();
        }

        [Theory]
        [InlineData("https://172.15.255.255/image.jpg")]
        [InlineData("https://172.32.0.1/image.jpg")]
        [InlineData("https://192.167.255.255/image.jpg")]
        [InlineData("https://192.169.0.1/image.jpg")]
        [InlineData("https://169.253.255.255/image.jpg")]
        [InlineData("https://169.255.0.1/image.jpg")]
        public void TryValidatePublicHttpsUrl_with_ipv4_just_outside_blocked_ranges_returns_true(
            string input)
        {
           
            var valid = SsrfUrlValidator.TryValidatePublicHttpsUrl(
                input,
                out var error);

          
            valid.Should().BeTrue();
            error.Should().BeEmpty();
        }
    }
}