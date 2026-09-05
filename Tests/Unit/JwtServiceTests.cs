using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Obrasci.Models;
using Obrasci.Services;
using Xunit;

namespace Tests.Unit
{
    public class JwtServiceTests
    {
        private const string JwtKey =
            "this-is-a-test-key-that-is-long-enough-for-hmac-sha256";

        private const string JwtIssuer = "Obrasci.Tests";
        private const string JwtAudience = "Obrasci.Tests.Client";

        [Fact]
        public async Task CreateAccessTokenAsync_with_valid_configuration_creates_valid_signed_token()
        {
            
            var user = CreateUser();
            var userManager = CreateUserManager();
            var service = new JwtService(
                CreateConfiguration(),
                userManager.Object);

            userManager
                .Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "User" });

            
            var (token, expiresAtUtc) = await service.CreateAccessTokenAsync(user);

            token.Should().NotBeNullOrWhiteSpace();
            expiresAtUtc.Should().BeCloseTo(
                DateTime.UtcNow.AddMinutes(15),
                TimeSpan.FromSeconds(5));

            var handler = new JwtSecurityTokenHandler();

            var principal = handler.ValidateToken(
                token,
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = JwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = JwtAudience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(JwtKey)),
                    ClockSkew = TimeSpan.Zero
                },
                out var securityToken);

            securityToken.Should().BeOfType<JwtSecurityToken>();

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            jwt.Subject.Should().Be(user.Id);

            jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value
                .Should().Be(user.Email);

            jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.UniqueName).Value
                .Should().Be(user.UserName);

            jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value
                .Should().Be("User");

            jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value
                .Should().NotBeNullOrWhiteSpace();

            jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Iat).Value
                .Should().NotBeNullOrWhiteSpace();

            userManager.Verify(
                x => x.GetRolesAsync(user),
                Times.Once);
        }

        [Fact]
        public async Task CreateAccessTokenAsync_adds_every_role_to_token()
        {
            
            var user = CreateUser();
            var userManager = CreateUserManager();

            userManager
                .Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string>
                {
                    "User",
                    "Administrator"
                });

            var service = new JwtService(
                CreateConfiguration(),
                userManager.Object);

            
            var (token, _) = await service.CreateAccessTokenAsync(user);

            
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            jwt.Claims
                .Where(claim => claim.Type == ClaimTypes.Role)
                .Select(claim => claim.Value)
                .Should()
                .BeEquivalentTo("User", "Administrator");
        }

        [Fact]
        public async Task CreateAccessTokenAsync_when_signing_key_is_missing_throws_expected_exception()
        {
           
            var userManager = CreateUserManager();

            var configuration = CreateConfiguration(
                includeKey: false,
                includeIssuer: true,
                includeAudience: true);

            var service = new JwtService(
                configuration,
                userManager.Object);

           
            Func<Task> act = () => service.CreateAccessTokenAsync(CreateUser());

            await act.Should()
                .ThrowAsync<InvalidOperationException>()
                .WithMessage("JWT signing key is missing.");
        }

        [Fact]
        public async Task CreateAccessTokenAsync_when_issuer_is_missing_throws_expected_exception()
        {
            
            var userManager = CreateUserManager();

            var configuration = CreateConfiguration(
                includeKey: true,
                includeIssuer: false,
                includeAudience: true);

            var service = new JwtService(
                configuration,
                userManager.Object);

            
            Func<Task> act = () => service.CreateAccessTokenAsync(CreateUser());

           
            await act.Should()
                .ThrowAsync<InvalidOperationException>()
                .WithMessage("JWT issuer is missing.");
        }

        [Fact]
        public async Task CreateAccessTokenAsync_when_audience_is_missing_throws_expected_exception()
        {
            
            var userManager = CreateUserManager();

            var configuration = CreateConfiguration(
                includeKey: true,
                includeIssuer: true,
                includeAudience: false);

            var service = new JwtService(
                configuration,
                userManager.Object);

            
            Func<Task> act = () => service.CreateAccessTokenAsync(CreateUser());

            
            await act.Should()
                .ThrowAsync<InvalidOperationException>()
                .WithMessage("JWT audience is missing.");
        }

        [Fact]
        public async Task CreateAccessTokenAsync_when_access_token_minutes_is_missing_uses_15_minute_default()
        {
            
            var user = CreateUser();
            var userManager = CreateUserManager();

            userManager
                .Setup(x => x.GetRolesAsync(user))
                .ReturnsAsync(new List<string>());

            var configuration = CreateConfiguration(
                includeAccessTokenMinutes: false);

            var service = new JwtService(
                configuration,
                userManager.Object);

            var before = DateTime.UtcNow;

            
            var (_, expiresAtUtc) = await service.CreateAccessTokenAsync(user);

            var after = DateTime.UtcNow;

            
            expiresAtUtc.Should().BeOnOrAfter(before.AddMinutes(15));
            expiresAtUtc.Should().BeOnOrBefore(after.AddMinutes(15));
        }

        [Fact]
        public void HashToken_returns_sha256_hash_as_uppercase_hex()
        {
            
            const string rawToken = "known-token";

            var service = new JwtService(
                CreateConfiguration(),
                CreateUserManager().Object);

            var expectedHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    Encoding.UTF8.GetBytes(rawToken)));

            
            var result = service.HashToken(rawToken);

            
            result.Should().Be(expectedHash);
            result.Should().HaveLength(64);
            result.Should().MatchRegex("^[0-9A-F]{64}$");
        }

        [Fact]
        public void HashToken_returns_same_hash_for_same_raw_token()
        {
           
            var service = new JwtService(
                CreateConfiguration(),
                CreateUserManager().Object);

            var first = service.HashToken("same-token");
            var second = service.HashToken("same-token");

            
            first.Should().Be(second);
        }

        [Fact]
        public void HashToken_returns_different_hashes_for_different_raw_tokens()
        {
            
            var service = new JwtService(
                CreateConfiguration(),
                CreateUserManager().Object);

           
            var first = service.HashToken("token-one");
            var second = service.HashToken("token-two");

           
            first.Should().NotBe(second);
        }

        [Fact]
        public void CreateRefreshToken_returns_base64_raw_token_and_matching_hash()
        {
            
            var service = new JwtService(
                CreateConfiguration(),
                CreateUserManager().Object);

            
            var (rawToken, tokenHash) = service.CreateRefreshToken();

           
            rawToken.Should().NotBeNullOrWhiteSpace();

            Action decode = () => Convert.FromBase64String(rawToken);
            decode.Should().NotThrow();

            Convert.FromBase64String(rawToken)
                .Should()
                .HaveCount(64);

            tokenHash.Should().Be(service.HashToken(rawToken));
            tokenHash.Should().HaveLength(64);
        }

        [Fact]
        public void CreateRefreshToken_generates_unique_tokens()
        {
            
            var service = new JwtService(
                CreateConfiguration(),
                CreateUserManager().Object);

          
            var first = service.CreateRefreshToken();
            var second = service.CreateRefreshToken();

          
            first.RawToken.Should().NotBe(second.RawToken);
            first.TokenHash.Should().NotBe(second.TokenHash);
        }

        private static ApplicationUser CreateUser()
        {
            return new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "jwt-test-user",
                Email = "jwt-test@example.test"
            };
        }

        private static IConfiguration CreateConfiguration(
            bool includeKey = true,
            bool includeIssuer = true,
            bool includeAudience = true,
            bool includeAccessTokenMinutes = true)
        {
            var settings = new Dictionary<string, string?>();

            if (includeKey)
            {
                settings["Jwt:Key"] = JwtKey;
            }

            if (includeIssuer)
            {
                settings["Jwt:Issuer"] = JwtIssuer;
            }

            if (includeAudience)
            {
                settings["Jwt:Audience"] = JwtAudience;
            }

            if (includeAccessTokenMinutes)
            {
                settings["Jwt:AccessTokenMinutes"] = "15";
            }

            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }

        private static Mock<UserManager<ApplicationUser>> CreateUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();

            return new Mock<UserManager<ApplicationUser>>(
                store.Object,
                new Mock<IOptions<IdentityOptions>>().Object,
                new Mock<IPasswordHasher<ApplicationUser>>().Object,
                Array.Empty<IUserValidator<ApplicationUser>>(),
                Array.Empty<IPasswordValidator<ApplicationUser>>(),
                new Mock<ILookupNormalizer>().Object,
                new IdentityErrorDescriber(),
                new Mock<IServiceProvider>().Object,
                new Mock<ILogger<UserManager<ApplicationUser>>>().Object);
        }
    }
}