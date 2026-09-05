using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Moq;
using Obrasci.Controllers;
using Obrasci.Models;
using Obrasci.Services;
using System.Reflection;
using System.Text;
using Xunit;

namespace Tests.Unit
{
    public class SerializationDemoControllerTests
    {
        private readonly Mock<IPhotoSnapshotService> _snapshotService = new();
        private readonly SerializationDemoController _controller;

        public SerializationDemoControllerTests()
        {
            _controller = new SerializationDemoController(
                _snapshotService.Object);
        }

        [Fact]
        public void ExportPhoto_requires_authorized_user()
        {
           
            var method = typeof(SerializationDemoController)
                .GetMethod(nameof(SerializationDemoController.ExportPhoto));

            var attribute = method!
                .GetCustomAttribute<AuthorizeAttribute>();

        
            attribute.Should().NotBeNull();
        }

        [Fact]
        public async Task ExportPhoto_when_photo_does_not_exist_returns_not_found_with_message()
        {
         
            var photoId = Guid.NewGuid();

            _snapshotService
                .Setup(service => service.ExportPhotoAsync(photoId))
                .ReturnsAsync((string?)null);

          
            var result = await _controller.ExportPhoto(photoId);

         
            var notFound = result.Should()
                .BeOfType<NotFoundObjectResult>()
                .Subject;

            notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);

            GetAnonymousProperty(notFound.Value!, "message")
                .Should()
                .Be("Photo was not found.");

            _snapshotService.Verify(
                service => service.ExportPhotoAsync(photoId),
                Times.Once);
        }

        [Fact]
        public async Task ExportPhoto_when_photo_exists_returns_success_json_response()
        {
            
            var photoId = Guid.NewGuid();

            _snapshotService
                .Setup(service => service.ExportPhotoAsync(photoId))
                .ReturnsAsync("photo-export.json");

       
            var result = await _controller.ExportPhoto(photoId);

      
            var ok = result.Should()
                .BeOfType<OkObjectResult>()
                .Subject;

            ok.StatusCode.Should().Be(StatusCodes.Status200OK);

            GetAnonymousProperty(ok.Value!, "message")
                .Should()
                .Be("Photo export snapshot was serialized successfully.");

            GetAnonymousProperty(ok.Value!, "fileName")
                .Should()
                .Be("photo-export.json");

            GetAnonymousProperty(ok.Value!, "format")
                .Should()
                .Be("JSON");

            GetAnonymousProperty(ok.Value!, "storageLocation")
                .Should()
                .Be("App_Data/serialized");
        }

        [Fact]
        public void ImportSnapshot_accepts_multipart_form_data()
        {
            
            var method = typeof(SerializationDemoController)
                .GetMethod(nameof(SerializationDemoController.ImportSnapshot));

            var attribute = method!
                .GetCustomAttribute<ConsumesAttribute>();


            attribute.Should().NotBeNull();

            attribute!.ContentTypes.Should()
                .Contain("multipart/form-data");
        }

        [Fact]
        public void ImportSnapshot_has_one_mb_request_size_limit()
        {
    
            var method = typeof(SerializationDemoController)
                .GetMethod(nameof(SerializationDemoController.ImportSnapshot));

            var attribute = method!
                .GetCustomAttribute<RequestSizeLimitAttribute>();

            attribute.Should().NotBeNull();

            var sizeLimitMetadata = attribute!
                .Should()
                .BeAssignableTo<IRequestSizeLimitMetadata>()
                .Subject;

            sizeLimitMetadata.MaxRequestBodySize.Should().Be(1_048_576);
        }

        [Fact]
        public async Task ImportSnapshot_with_valid_whitelisted_json_returns_success_response()
        {
            
            const string json = """
    {
      "PhotoId": "8e01352e-9650-4034-9fc4-555ca9f02fff",
      "FileName": "test-photo.jpg",
      "ExportedAtUtc": "2026-09-03T10:00:00Z"
    }
    """;

            var file = CreateFormFile(
                json,
                "valid-snapshot.json",
                "application/json");

      
            var result = await _controller.ImportSnapshot(
                file,
                "PhotoExportSnapshot",
                CancellationToken.None);

    
            var ok = result.Should()
                .BeOfType<OkObjectResult>()
                .Subject;

            ok.StatusCode.Should().Be(StatusCodes.Status200OK);

            GetAnonymousProperty(ok.Value!, "message")
                .Should()
                .Be("Whitelisted snapshot deserialized successfully.");

            GetAnonymousProperty(ok.Value!, "type")
                .Should()
                .Be(nameof(PhotoExportSnapshot));

            GetAnonymousProperty(ok.Value!, "PhotoId")
                .Should()
                .Be(Guid.Parse(
                    "8e01352e-9650-4034-9fc4-555ca9f02fff"));

            GetAnonymousProperty(ok.Value!, "FileName")
                .Should()
                .Be("test-photo.jpg");

            var exportedAtUtc = GetAnonymousProperty(
         ok.Value!,
         "ExportedAtUtc")
     .Should()
     .BeOfType<DateTime>()
     .Subject;

            exportedAtUtc.Should().Be(
                new DateTime(
                    2026,
                    9,
                    3,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc));
        }

        [Fact]
        public async Task ImportSnapshot_with_plain_text_returns_bad_request_and_rejection_reason()
        {
            
            var file = CreateFormFile(
                "this is not JSON",
                "invalid-snapshot.json",
                "application/json");

         
            var result = await _controller.ImportSnapshot(
                file,
                "PhotoExportSnapshot",
                CancellationToken.None);

       
            var badRequest = result.Should()
                .BeOfType<BadRequestObjectResult>()
                .Subject;

            badRequest.StatusCode.Should().Be(
                StatusCodes.Status400BadRequest);

            GetAnonymousProperty(badRequest.Value!, "message")
                .Should()
                .Be("Snapshot import rejected.");

            GetAnonymousProperty(badRequest.Value!, "reason")
                .Should()
                .Be("The uploaded file is not a JSON object or array.");
        }

        [Fact]
        public async Task ImportSnapshot_with_disallowed_requested_type_returns_bad_request()
        {
           
            const string json = """
            {
              "photoId": "8e01352e-9650-4034-9fc4-555ca9f02fff",
              "fileName": "test-photo.jpg",
              "exportedAtUtc": "2026-09-03T10:00:00Z"
            }
            """;

            var file = CreateFormFile(
                json,
                "valid-looking-snapshot.json",
                "application/json");

      
            var result = await _controller.ImportSnapshot(
                file,
                "ApplicationUser",
                CancellationToken.None);

   
            var badRequest = result.Should()
                .BeOfType<BadRequestObjectResult>()
                .Subject;

            GetAnonymousProperty(badRequest.Value!, "message")
                .Should()
                .Be("Snapshot import rejected.");

            GetAnonymousProperty(badRequest.Value!, "reason")
                .Should()
                .Be(
                    "Type 'ApplicationUser' is not allowed for deserialization.");
        }

        [Fact]
        public async Task ImportSnapshot_with_malformed_json_returns_bad_request()
        {
           
            var file = CreateFormFile(
                """{"photoId": }""",
                "malformed-snapshot.json",
                "application/json");

        
            var result = await _controller.ImportSnapshot(
                file,
                "PhotoExportSnapshot",
                CancellationToken.None);

        
            var badRequest = result.Should()
                .BeOfType<BadRequestObjectResult>()
                .Subject;

            GetAnonymousProperty(badRequest.Value!, "message")
                .Should()
                .Be("Snapshot import rejected.");

            GetAnonymousProperty(badRequest.Value!, "reason")
                .Should()
                .Be("The uploaded file contains invalid JSON.");
        }

        [Fact]
        public async Task ImportSnapshot_with_empty_file_returns_bad_request()
        {
            
            var file = CreateFormFile(
                string.Empty,
                "empty-snapshot.json",
                "application/json");

           
            var result = await _controller.ImportSnapshot(
                file,
                "PhotoExportSnapshot",
                CancellationToken.None);

           
            var badRequest = result.Should()
                .BeOfType<BadRequestObjectResult>()
                .Subject;

            GetAnonymousProperty(badRequest.Value!, "reason")
                .Should()
                .Be("The uploaded file is empty.");
        }

        private static IFormFile CreateFormFile(
            string content,
            string fileName,
            string contentType)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);

            return new FormFile(
                stream,
                0,
                bytes.Length,
                "file",
                fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }

        private static object? GetAnonymousProperty(
            object value,
            string propertyName)
        {
            return value.GetType()
                .GetProperty(propertyName)?
                .GetValue(value);
        }
    }
}