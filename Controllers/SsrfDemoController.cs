using Microsoft.AspNetCore.Mvc;
using Obrasci.Services;

namespace Obrasci.Controllers;

[ApiController]
[Route("api/ssrf-demo")]
public class SsrfDemoController : ControllerBase
{
    [HttpPost("validate-url")]
    public IActionResult ValidateUrl([FromBody] UrlRequest request)
    {
        if (!SsrfUrlValidator.TryValidatePublicHttpsUrl(
                request.Url,
                out var error))
        {
            return BadRequest(new
            {
                message = "URL rejected by SSRF protection.",
                reason = error
            });
        }

        return Ok(new
        {
            message = "URL passed SSRF validation.",
            url = request.Url
        });
    }
}

public sealed class UrlRequest
{
    public string? Url { get; set; }
}