using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Obrasci.Data;

namespace Obrasci.Controllers;

[ApiController]
[Route("api/sql-injection-demo")]
public class SqlInjectionControlledDemoController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SqlInjectionControlledDemoController(ApplicationDbContext context)
    {
        _context = context;
    }


    [HttpGet("safe-search")]
    public async Task<IActionResult> SafeSearch([FromQuery] string? term)
    {
        term ??= string.Empty;

        if (term.Length > 100)
        {
            return BadRequest(new
            {
                message = "Search term must be 100 characters or fewer."
            });
        }

        var results = await _context.Photos
            .AsNoTracking()
            .Where(photo =>
                photo.FileName.Contains(term) ||
                (photo.Description != null &&
                 photo.Description.Contains(term)) ||
                (photo.Hashtags != null &&
                 photo.Hashtags.Contains(term)))
            .OrderByDescending(photo => photo.UploadedAt)
            .Take(20)
            .Select(photo => new
            {
                photo.Id,
                photo.FileName,
                photo.Description,
                photo.Hashtags,
                photo.UploadedAt
            })
            .ToListAsync();

        return Ok(new
        {
            searchedTerm = term,
            resultCount = results.Count,
            results
        });
    }
}