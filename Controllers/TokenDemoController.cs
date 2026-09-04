using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Obrasci.Controllers;

[ApiController]
[Route("api/token-demo")]
public class TokenDemoController : ControllerBase
{
    [HttpGet("public")]
    public IActionResult PublicEndpoint()
    {
        return Ok(new
        {
            message = "This is a public endpoint."
        });
    }
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpGet("me")]
    public IActionResult MyProfile()
    {
        return Ok(new
        {
            message = "JWT access token accepted.",
            userId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            email = User.FindFirstValue(ClaimTypes.Email),
            username = User.Identity?.Name,
            roles = User.FindAll(ClaimTypes.Role)
                .Select(claim => claim.Value)
                .ToList()
        });
    }
}