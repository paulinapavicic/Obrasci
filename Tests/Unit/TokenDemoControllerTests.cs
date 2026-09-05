using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Obrasci.Controllers;
using Xunit;

namespace Tests.Unit
{
    public class TokenDemoControllerTests
    {
        private readonly TokenDemoController _controller = new();

        [Fact]
        public void Controller_has_expected_api_route()
        {
           
            var route = typeof(TokenDemoController)
                .GetCustomAttribute<RouteAttribute>();

           
            route.Should().NotBeNull();
            route!.Template.Should().Be("api/token-demo");
        }

        [Fact]
        public void PublicEndpoint_has_expected_get_route()
        {
            
            var method = typeof(TokenDemoController)
                .GetMethod(nameof(TokenDemoController.PublicEndpoint));

            var route = method!
                .GetCustomAttribute<HttpGetAttribute>();

            
            route.Should().NotBeNull();
            route!.Template.Should().Be("public");
        }

        [Fact]
        public void MyProfile_has_expected_get_route()
        {
           
            var method = typeof(TokenDemoController)
                .GetMethod(nameof(TokenDemoController.MyProfile));

            var route = method!
                .GetCustomAttribute<HttpGetAttribute>();

           
            route.Should().NotBeNull();
            route!.Template.Should().Be("me");
        }

        [Fact]
        public void MyProfile_requires_jwt_bearer_authentication()
        {
        
            var method = typeof(TokenDemoController)
                .GetMethod(nameof(TokenDemoController.MyProfile));

            var attribute = method!
                .GetCustomAttribute<AuthorizeAttribute>();

            
            attribute.Should().NotBeNull();
            attribute!.AuthenticationSchemes.Should()
                .Be(JwtBearerDefaults.AuthenticationScheme);
        }

        [Fact]
        public void PublicEndpoint_returns_ok_with_public_message()
        {
            
            var result = _controller.PublicEndpoint();

            
            var ok = result.Should()
                .BeOfType<OkObjectResult>()
                .Subject;

            ok.StatusCode.Should()
                .Be(StatusCodes.Status200OK);

            GetProperty(ok.Value!, "message")
                .Should()
                .Be("This is a public endpoint.");
        }

        [Fact]
        public void MyProfile_with_authenticated_jwt_claims_returns_expected_profile()
        {
           
            SetCurrentUser(
                userId: "7f4fca51-5a31-4fe8-bca7-bf0c4cda0e01",
                email: "pauli@example.test",
                username: "pauli",
                roles: new[] { "User", "PhotoEditor" });

            
            var result = _controller.MyProfile();

       
            var ok = result.Should()
                .BeOfType<OkObjectResult>()
                .Subject;

            ok.StatusCode.Should()
                .Be(StatusCodes.Status200OK);

            GetProperty(ok.Value!, "message")
                .Should()
                .Be("JWT access token accepted.");

            GetProperty(ok.Value!, "userId")
                .Should()
                .Be("7f4fca51-5a31-4fe8-bca7-bf0c4cda0e01");

            GetProperty(ok.Value!, "email")
                .Should()
                .Be("pauli@example.test");

            GetProperty(ok.Value!, "username")
                .Should()
                .Be("pauli");

            GetStringListProperty(ok.Value!, "roles")
                .Should()
                .Equal("User", "PhotoEditor");
        }

        [Fact]
        public void MyProfile_with_no_roles_returns_empty_role_list()
        {
            
            SetCurrentUser(
                userId: "user-123",
                email: "no-roles@example.test",
                username: "no-roles",
                roles: Array.Empty<string>());

            
            var result = _controller.MyProfile();

         
            var ok = result.Should()
                .BeOfType<OkObjectResult>()
                .Subject;

            GetStringListProperty(ok.Value!, "roles")
                .Should()
                .BeEmpty();
        }

        [Fact]
        public void MyProfile_with_missing_optional_claims_returns_null_values()
        {
            
            SetPrincipal(new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(
                            ClaimTypes.NameIdentifier,
                            "user-123")
                    },
                    JwtBearerDefaults.AuthenticationScheme)));

           
            var result = _controller.MyProfile();

          
            var ok = result.Should()
                .BeOfType<OkObjectResult>()
                .Subject;

            GetProperty(ok.Value!, "userId")
                .Should()
                .Be("user-123");

            GetProperty(ok.Value!, "email")
                .Should()
                .BeNull();

            GetProperty(ok.Value!, "username")
                .Should()
                .BeNull();

            GetStringListProperty(ok.Value!, "roles")
                .Should()
                .BeEmpty();
        }

        [Fact]
        public void MyProfile_with_multiple_roles_returns_every_role()
        {
            
            SetCurrentUser(
                userId: "admin-001",
                email: "admin@example.test",
                username: "administrator",
                roles: new[]
                {
                    "User",
                    "Moderator",
                    "Administrator"
                });

            
            var result = _controller.MyProfile();

           
            var ok = result.Should()
                .BeOfType<OkObjectResult>()
                .Subject;

            GetStringListProperty(ok.Value!, "roles")
                .Should()
                .Equal(
                    "User",
                    "Moderator",
                    "Administrator");
        }

        private void SetCurrentUser(
            string userId,
            string email,
            string username,
            IEnumerable<string> roles)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId),
                new(ClaimTypes.Email, email),
                new(ClaimTypes.Name, username)
            };

            claims.AddRange(roles.Select(role =>
                new Claim(ClaimTypes.Role, role)));

            SetPrincipal(new ClaimsPrincipal(
                new ClaimsIdentity(
                    claims,
                    JwtBearerDefaults.AuthenticationScheme)));
        }

        private void SetPrincipal(ClaimsPrincipal principal)
        {
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal
                }
            };
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

        private static List<string> GetStringListProperty(
            object value,
            string propertyName)
        {
            var propertyValue = GetProperty(value, propertyName);

            if (propertyValue is not IEnumerable<string> roles)
            {
                throw new InvalidOperationException(
                    $"Property '{propertyName}' was missing or was not a list of strings.");
            }

            return roles.ToList();
        }
    }
}