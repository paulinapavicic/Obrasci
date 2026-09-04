using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Obrasci.Models;

namespace Obrasci.Services;

public class JwtService
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;

    public JwtService(
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager)
    {
        _configuration = configuration;
        _userManager = userManager;
    }

    public async Task<(string Token, DateTime ExpiresAtUtc)> CreateAccessTokenAsync(
        ApplicationUser user)
    {
        var key = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT signing key is missing.");

        var issuer = _configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("JWT issuer is missing.");

        var audience = _configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("JWT audience is missing.");

        var accessTokenMinutes = int.Parse(
            _configuration["Jwt:AccessTokenMinutes"] ?? "15");

        var now = DateTime.UtcNow;
        var expiresAtUtc = now.AddMinutes(accessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        var roles = await _userManager.GetRolesAsync(user);

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

        var credentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return (
            new JwtSecurityTokenHandler().WriteToken(jwt),
            expiresAtUtc);
    }

    public (string RawToken, string TokenHash) CreateRefreshToken()
    {
        var rawToken = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(64));

        return (rawToken, HashToken(rawToken));
    }

    public string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));

        return Convert.ToHexString(bytes);
    }
}