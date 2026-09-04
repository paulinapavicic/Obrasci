using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Obrasci.Models;
using Obrasci.Services;

namespace Obrasci.Controllers;

[ApiController]
[Route("api/serialization-demo")]
public class SerializationDemoController : ControllerBase
{
    private readonly IPhotoSnapshotService _photoSnapshotService;

    public SerializationDemoController(
        IPhotoSnapshotService photoSnapshotService)
    {
        _photoSnapshotService = photoSnapshotService;
    }
    [Authorize]
    [HttpPost("photos/{photoId:guid}/export")]
    public async Task<IActionResult> ExportPhoto(Guid photoId)
    {
        var fileName = await _photoSnapshotService
            .ExportPhotoAsync(photoId);

        if (fileName is null)
        {
            return NotFound(new
            {
                message = "Photo was not found."
            });
        }

        return Ok(new
        {
            message = "Photo export snapshot was serialized successfully.",
            fileName,
            format = "JSON",
            storageLocation = "App_Data/serialized"
        });
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> ImportSnapshot(
        IFormFile file,
        [FromForm] string requestedType,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var validationStream = file.OpenReadStream();

            await PhotoSnapshotFileValidator
                .ValidateJsonSnapshotAsync(
                    validationStream,
                    cancellationToken);

            var snapshot = await PhotoSnapshotDeserializer
                .DeserializeWhitelistedSnapshotAsync(
                    file,
                    requestedType,
                    cancellationToken);

            return Ok(new
            {
                message = "Whitelisted snapshot deserialized successfully.",
                type = nameof(PhotoExportSnapshot),
                snapshot.PhotoId,
                snapshot.FileName,
                snapshot.ExportedAtUtc
            });
        }
        catch (InvalidDataException exception)
        {
            return BadRequest(new
            {
                message = "Snapshot import rejected.",
                reason = exception.Message
            });
        }
    }
}