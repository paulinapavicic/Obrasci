using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Obrasci.Controllers;

[ApiController]
[Route("api/csrf-demo")]
public class CsrfDemoController : ControllerBase
{
    private readonly IAntiforgery _antiforgery;

    public CsrfDemoController(IAntiforgery antiforgery)
    {
        _antiforgery = antiforgery;
    }

    [HttpGet("token")]
    public IActionResult GetToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);

        return Ok(new
        {
            csrfToken = tokens.RequestToken
        });
    }

    [HttpPost("change-setting")]
    public async Task<IActionResult> ChangeSetting()
    {
        await _antiforgery.ValidateRequestAsync(HttpContext);

        return Ok(new
        {
            message = "State-changing request accepted because CSRF validation passed."
        });
    }
}