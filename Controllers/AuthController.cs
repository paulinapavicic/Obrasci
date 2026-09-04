using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Obrasci.Data;
using Obrasci.Dtos;
using Obrasci.Models;
using Obrasci.Services;

namespace Obrasci.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly JwtService _jwtService;
    private readonly IConfiguration _configuration;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        JwtService jwtService,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _context = context;
        _jwtService = jwtService;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user == null ||
            !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        var tokenResponse = await CreateTokenPairAsync(user, Guid.NewGuid().ToString());

        return Ok(tokenResponse);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh(RefreshRequest request)
    {
        var tokenHash = _jwtService.HashToken(request.RefreshToken);

        var storedToken = await _context.RefreshTokens
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash);

        if (storedToken == null)
        {
            return Unauthorized(new
            {
                message = "Invalid refresh token."
            });
        }

        if (storedToken.RevokedAtUtc != null)
        {
            await RevokeTokenFamilyAsync(storedToken.FamilyId);

            return Unauthorized(new
            {
                message = "Refresh-token reuse detected. Please log in again."
            });
        }

        if (storedToken.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return Unauthorized(new
            {
                message = "Refresh token has expired. Please log in again."
            });
        }

        var user = await _userManager.FindByIdAsync(storedToken.UserId);

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "User no longer exists."
            });
        }

        storedToken.RevokedAtUtc = DateTime.UtcNow;

        var tokenResponse = await CreateTokenPairAsync(user, storedToken.FamilyId);

        var newTokenHash = _jwtService.HashToken(tokenResponse.RefreshToken);
        storedToken.ReplacedByTokenHash = newTokenHash;

        await _context.SaveChangesAsync();

        return Ok(tokenResponse);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request)
    {
        var tokenHash = _jwtService.HashToken(request.RefreshToken);

        var storedToken = await _context.RefreshTokens
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash);

        if (storedToken != null)
        {
            await RevokeTokenFamilyAsync(storedToken.FamilyId);
        }

        return Ok(new
        {
            message = "Logged out. Refresh-token family was revoked."
        });
    }

    private async Task<TokenResponse> CreateTokenPairAsync(
        ApplicationUser user,
        string familyId)
    {
        var (accessToken, accessTokenExpiresAtUtc) =
            await _jwtService.CreateAccessTokenAsync(user);

        var (rawRefreshToken, refreshTokenHash) =
            _jwtService.CreateRefreshToken();

        var refreshTokenDays = int.Parse(
            _configuration["Jwt:RefreshTokenDays"] ?? "7");

        var refreshTokenExpiresAtUtc =
            DateTime.UtcNow.AddDays(refreshTokenDays);

        _context.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = refreshTokenHash,
            UserId = user.Id,
            FamilyId = familyId,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = refreshTokenExpiresAtUtc
        });

        await _context.SaveChangesAsync();

        return new TokenResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc,
            RefreshToken = rawRefreshToken,
            RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc
        };
    }

    private async Task RevokeTokenFamilyAsync(string familyId)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(token =>
                token.FamilyId == familyId &&
                token.RevokedAtUtc == null)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }
}