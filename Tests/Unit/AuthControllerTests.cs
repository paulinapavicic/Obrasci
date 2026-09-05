using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Obrasci.Controllers;
using Obrasci.Data;
using Obrasci.Dtos;
using Obrasci.Models;
using Obrasci.Services;
using Xunit;

namespace Tests.Unit
{
    public class AuthControllerTests : IDisposable
    {
        private const string JwtKey =
            "this-is-a-test-key-long-enough-for-hmac-sha256-signing";

        private readonly ApplicationDbContext _context;
        private readonly Mock<UserManager<ApplicationUser>> _userManager;
        private readonly JwtService _jwtService;
        private readonly IConfiguration _configuration;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);

            _userManager = CreateUserManager();

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = JwtKey,
                    ["Jwt:Issuer"] = "Obrasci.Tests",
                    ["Jwt:Audience"] = "Obrasci.Tests.Client",
                    ["Jwt:AccessTokenMinutes"] = "15",
                    ["Jwt:RefreshTokenDays"] = "7"
                })
                .Build();

            _jwtService = new JwtService(
                _configuration,
                _userManager.Object);

            _controller = new AuthController(
                _userManager.Object,
                _context,
                _jwtService,
                _configuration);
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        [Fact]
        public async Task Login_when_email_does_not_exist_returns_unauthorized()
        {
           
            var request = new LoginRequest
            {
                Email = "missing@example.test",
                Password = "Password123!"
            };

            _userManager
                .Setup(manager => manager.FindByEmailAsync(request.Email))
                .ReturnsAsync((ApplicationUser?)null);

         
            var result = await _controller.Login(request);

         
            var unauthorized = result.Result
                .Should()
                .BeOfType<UnauthorizedObjectResult>()
                .Subject;

            unauthorized.StatusCode.Should().Be(401);

            _userManager.Verify(
                manager => manager.CheckPasswordAsync(
                    It.IsAny<ApplicationUser>(),
                    It.IsAny<string>()),
                Times.Never);

            (await _context.RefreshTokens.CountAsync())
                .Should()
                .Be(0);
        }

        [Fact]
        public async Task Login_when_password_is_invalid_returns_unauthorized()
        {
          
            var user = CreateUser();

            var request = new LoginRequest
            {
                Email = user.Email!,
                Password = "wrong-password"
            };

            _userManager
                .Setup(manager => manager.FindByEmailAsync(request.Email))
                .ReturnsAsync(user);

            _userManager
                .Setup(manager => manager.CheckPasswordAsync(
                    user,
                    request.Password))
                .ReturnsAsync(false);

         
            var result = await _controller.Login(request);

         
            result.Result.Should().BeOfType<UnauthorizedObjectResult>();

            (await _context.RefreshTokens.CountAsync())
                .Should()
                .Be(0);
        }

        [Fact]
        public async Task Login_when_credentials_are_valid_returns_token_pair_and_stores_hashed_refresh_token()
        {
           
            var user = CreateUser();

            var request = new LoginRequest
            {
                Email = user.Email!,
                Password = "Password123!"
            };

            _userManager
                .Setup(manager => manager.FindByEmailAsync(request.Email))
                .ReturnsAsync(user);

            _userManager
                .Setup(manager => manager.CheckPasswordAsync(
                    user,
                    request.Password))
                .ReturnsAsync(true);

            _userManager
                .Setup(manager => manager.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "User" });

            var before = DateTime.UtcNow;

           
            var result = await _controller.Login(request);

            var ok = result.Result
                .Should()
                .BeOfType<OkObjectResult>()
                .Subject;

            var response = ok.Value
                .Should()
                .BeOfType<TokenResponse>()
                .Subject;

            response.AccessToken.Should().NotBeNullOrWhiteSpace();
            response.RefreshToken.Should().NotBeNullOrWhiteSpace();
            response.AccessTokenExpiresAtUtc.Should().BeAfter(before);
            response.RefreshTokenExpiresAtUtc.Should().BeAfter(before);

            var handler = new JwtSecurityTokenHandler();

            handler.CanReadToken(response.AccessToken)
                .Should()
                .BeTrue();

            var storedToken = await _context.RefreshTokens.SingleAsync();

            storedToken.UserId.Should().Be(user.Id);
            storedToken.TokenHash.Should().Be(
                _jwtService.HashToken(response.RefreshToken));

            storedToken.TokenHash.Should().NotBe(response.RefreshToken);
            storedToken.FamilyId.Should().NotBeNullOrWhiteSpace();
            storedToken.RevokedAtUtc.Should().BeNull();

            _userManager.Verify(
                manager => manager.GetRolesAsync(user),
                Times.Once);
        }

        [Fact]
        public async Task Refresh_when_token_does_not_exist_returns_unauthorized()
        {
            
            var request = new RefreshRequest
            {
                RefreshToken = "unknown-refresh-token"
            };

            
            var result = await _controller.Refresh(request);

           
            var unauthorized = result.Result
                .Should()
                .BeOfType<UnauthorizedObjectResult>()
                .Subject;

            unauthorized.StatusCode.Should().Be(401);
        }

        [Fact]
        public async Task Refresh_when_token_is_expired_returns_unauthorized()
        {
            var rawToken = "expired-refresh-token";

            var storedToken = CreateRefreshToken(
                rawToken,
                expiresAtUtc: DateTime.UtcNow.AddMinutes(-1));

            _context.RefreshTokens.Add(storedToken);
            await _context.SaveChangesAsync();

            
            var result = await _controller.Refresh(new RefreshRequest
            {
                RefreshToken = rawToken
            });

           
            result.Result.Should().BeOfType<UnauthorizedObjectResult>();

            storedToken.RevokedAtUtc.Should().BeNull();
        }

        [Fact]
        public async Task Refresh_when_token_has_already_been_revoked_revokes_entire_family_and_returns_unauthorized()
        {
            
            const string familyId = "family-1";

            var reusedRawToken = "reused-token";
            var activeSiblingRawToken = "active-sibling-token";

            var reusedToken = CreateRefreshToken(
                reusedRawToken,
                familyId: familyId,
                expiresAtUtc: DateTime.UtcNow.AddDays(1),
                revokedAtUtc: DateTime.UtcNow.AddMinutes(-1));

            var activeSibling = CreateRefreshToken(
                activeSiblingRawToken,
                familyId: familyId,
                expiresAtUtc: DateTime.UtcNow.AddDays(1));

            _context.RefreshTokens.AddRange(reusedToken, activeSibling);
            await _context.SaveChangesAsync();

           
            var result = await _controller.Refresh(new RefreshRequest
            {
                RefreshToken = reusedRawToken
            });

           
            result.Result.Should().BeOfType<UnauthorizedObjectResult>();

            var persistedSibling = await _context.RefreshTokens
                .SingleAsync(token => token.TokenHash == activeSibling.TokenHash);

            persistedSibling.RevokedAtUtc.Should().NotBeNull();
        }

        [Fact]
        public async Task Refresh_when_user_no_longer_exists_returns_unauthorized()
        {
            
            var rawToken = "user-missing-token";

            var storedToken = CreateRefreshToken(
                rawToken,
                userId: "deleted-user-id",
                expiresAtUtc: DateTime.UtcNow.AddDays(1));

            _context.RefreshTokens.Add(storedToken);
            await _context.SaveChangesAsync();

            _userManager
                .Setup(manager => manager.FindByIdAsync("deleted-user-id"))
                .ReturnsAsync((ApplicationUser?)null);

            
            var result = await _controller.Refresh(new RefreshRequest
            {
                RefreshToken = rawToken
            });

           
            result.Result.Should().BeOfType<UnauthorizedObjectResult>();

            storedToken.RevokedAtUtc.Should().BeNull();
        }

        [Fact]
        public async Task Refresh_with_valid_token_rotates_token_revokes_old_one_and_returns_new_pair()
        {
            var user = CreateUser();

            const string familyId = "rotation-family";
            const string oldRawToken = "old-refresh-token";

            var oldStoredToken = CreateRefreshToken(
                oldRawToken,
                userId: user.Id,
                familyId: familyId,
                expiresAtUtc: DateTime.UtcNow.AddDays(1));

            _context.RefreshTokens.Add(oldStoredToken);
            await _context.SaveChangesAsync();

            _userManager
                .Setup(manager => manager.FindByIdAsync(user.Id))
                .ReturnsAsync(user);

            _userManager
                .Setup(manager => manager.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "User" });

            
            var result = await _controller.Refresh(new RefreshRequest
            {
                RefreshToken = oldRawToken
            });

            var ok = result.Result
                .Should()
                .BeOfType<OkObjectResult>()
                .Subject;

            var response = ok.Value
                .Should()
                .BeOfType<TokenResponse>()
                .Subject;

            response.RefreshToken.Should().NotBe(oldRawToken);

            var oldPersistedToken = await _context.RefreshTokens
                .SingleAsync(token => token.TokenHash == oldStoredToken.TokenHash);

            oldPersistedToken.RevokedAtUtc.Should().NotBeNull();
            oldPersistedToken.ReplacedByTokenHash.Should().Be(
                _jwtService.HashToken(response.RefreshToken));

            var replacement = await _context.RefreshTokens
                .SingleAsync(token =>
                    token.TokenHash ==
                    _jwtService.HashToken(response.RefreshToken));

            replacement.FamilyId.Should().Be(familyId);
            replacement.UserId.Should().Be(user.Id);
            replacement.RevokedAtUtc.Should().BeNull();

            (await _context.RefreshTokens.CountAsync())
                .Should()
                .Be(2);
        }

        [Fact]
        public async Task Logout_when_token_exists_revokes_all_active_tokens_in_family()
        {
           
            const string familyId = "logout-family";

            var rawTokenOne = "logout-token-one";
            var rawTokenTwo = "logout-token-two";

            var first = CreateRefreshToken(
                rawTokenOne,
                familyId: familyId,
                expiresAtUtc: DateTime.UtcNow.AddDays(1));

            var second = CreateRefreshToken(
                rawTokenTwo,
                familyId: familyId,
                expiresAtUtc: DateTime.UtcNow.AddDays(1));

            _context.RefreshTokens.AddRange(first, second);
            await _context.SaveChangesAsync();

           
            var result = await _controller.Logout(new RefreshRequest
            {
                RefreshToken = rawTokenOne
            });

         
            result.Should().BeOfType<OkObjectResult>();

            var familyTokens = await _context.RefreshTokens
                .Where(token => token.FamilyId == familyId)
                .ToListAsync();

            familyTokens.Should().HaveCount(2);

            familyTokens.All(token => token.RevokedAtUtc is not null)
                .Should()
                .BeTrue();
        }

        [Fact]
        public async Task Logout_when_token_does_not_exist_still_returns_ok()
        {
            
            var result = await _controller.Logout(new RefreshRequest
            {
                RefreshToken = "unknown-token"
            });

            result.Should().BeOfType<OkObjectResult>();
        }

        private ApplicationUser CreateUser()
        {
            return new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "auth-test-user",
                Email = "auth-test@example.test",
                Package = PackageType.Free
            };
        }

        private RefreshToken CreateRefreshToken(
            string rawToken,
            string? userId = null,
            string familyId = "family-default",
            DateTime? expiresAtUtc = null,
            DateTime? revokedAtUtc = null)
        {
            return new RefreshToken
            {
                TokenHash = _jwtService.HashToken(rawToken),
                UserId = userId ?? "user-default",
                FamilyId = familyId,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                ExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddDays(1),
                RevokedAtUtc = revokedAtUtc
            };
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