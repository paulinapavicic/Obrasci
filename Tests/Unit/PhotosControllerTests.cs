using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Obrasci.Controllers;
using Obrasci.Data;
using Obrasci.Models;
using Obrasci.Services;
using Obrasci.ViewModels;
using Xunit;

namespace Tests.Unit
{
    public class PhotosControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<IPhotoService> _photoService = new();
        private readonly Mock<UserManager<ApplicationUser>> _userManager;
        private readonly Mock<IActionLogger> _actionLogger = new();
        private readonly PhotosController _controller;

        public PhotosControllerTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _userManager = CreateUserManager();

            _controller = new PhotosController(
                _photoService.Object,
                _userManager.Object,
                _context,
                _actionLogger.Object);

            SetCurrentUser(authenticated: false);
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        [Fact]
        public async Task Index_returns_latest_ten_photos_for_anonymous_user()
        {
        
            var photos = new[]
            {
                CreatePhoto("user-1"),
                CreatePhoto("user-2")
            };

            _photoService
                .Setup(service => service.GetLastAsync(10))
                .ReturnsAsync(photos);

            SetCurrentUser(authenticated: false);

   
            var result = await _controller.Index();

      
            var view = result.Should().BeOfType<ViewResult>().Subject;

            var model = view.Model
                .Should()
                .BeAssignableTo<IEnumerable<Photo>>()
                .Subject
                .ToList();

            model.Should().HaveCount(2);

            _photoService.Verify(
                service => service.GetLastAsync(10),
                Times.Once);

            _userManager.Verify(
                manager => manager.GetUserAsync(
                    It.IsAny<ClaimsPrincipal>()),
                Times.Never);
        }

        [Fact]
        public async Task Index_when_authenticated_sets_package_and_daily_count()
        {
         
            var user = CreateUser(
                id: "user-1",
                email: "user@example.test",
                package: PackageType.Pro);

            user.DailyUploadCount = 4;

            _photoService
                .Setup(service => service.GetLastAsync(10))
                .ReturnsAsync(Array.Empty<Photo>());

            _userManager
                .Setup(manager => manager.GetUserAsync(
                    It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            SetCurrentUser(authenticated: true);

      
            var result = await _controller.Index();

    
            result.Should().BeOfType<ViewResult>();

            ((string?)_controller.ViewData["Package"])
                .Should()
                .Be(PackageType.Pro.ToString());

            ((int?)_controller.ViewData["DailyCount"])
                .Should()
                .Be(4);
        }

        [Fact]
        public void Upload_get_returns_view()
        {
        
            var result = _controller.Upload();

          
            result.Should().BeOfType<ViewResult>();
        }

        [Fact]
        public async Task Upload_post_when_user_is_not_authenticated_sets_error_and_redirects_login()
        {
         
            SetCurrentUser(authenticated: false);

            var file = CreateFormFile(
                "photo.jpg",
                "image/jpeg",
                new byte[] { 1, 2, 3 });

        
            var result = await _controller.Upload(
                file,
                "Description",
                "#tag",
                "Original");

      
            _controller.TempData["Error"]
                .Should()
                .Be("You must be logged in to upload photos.");

            var redirect = result
                .Should()
                .BeOfType<RedirectToActionResult>()
                .Subject;

            redirect.ActionName.Should().Be("Login");
            redirect.ControllerName.Should().Be("Account");

            _photoService.Verify(
                service => service.UploadAsync(
                    It.IsAny<ApplicationUser>(),
                    It.IsAny<IFormFile>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>()),
                Times.Never);
        }

        [Fact]
        public async Task Upload_post_when_file_is_null_returns_view_with_model_error()
        {
          
            SetCurrentUser(authenticated: true);

        
            var result = await _controller.Upload(
                null!,
                "Description",
                "#tag",
                "Original");

      
            result.Should().BeOfType<ViewResult>();

            _controller.ModelState["photoFile"]!.Errors
                .Should()
                .ContainSingle(error =>
                    error.ErrorMessage == "Photo file is required.");

            _photoService.Verify(
                service => service.UploadAsync(
                    It.IsAny<ApplicationUser>(),
                    It.IsAny<IFormFile>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>()),
                Times.Never);
        }

        [Fact]
        public async Task Upload_post_when_file_is_empty_returns_view_with_model_error()
        {
            SetCurrentUser(authenticated: true);

            var emptyFile = CreateFormFile(
                "empty.jpg",
                "image/jpeg",
                Array.Empty<byte>());

        
            var result = await _controller.Upload(
                emptyFile,
                null,
                null,
                "Original");

         
            result.Should().BeOfType<ViewResult>();

            _controller.ModelState["photoFile"]!.Errors
                .Should()
                .ContainSingle(error =>
                    error.ErrorMessage == "Photo file is required.");
        }

        [Fact]
        public async Task Upload_post_when_identity_user_is_missing_returns_challenge()
        {
            
            SetCurrentUser(authenticated: true);

            var file = CreateFormFile(
                "photo.jpg",
                "image/jpeg",
                new byte[] { 1, 2, 3 });

            _userManager
                .Setup(manager => manager.GetUserAsync(
                    It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync((ApplicationUser?)null);

         
            var result = await _controller.Upload(
                file,
                "Description",
                "#tag",
                "Original");

         
            result.Should().BeOfType<ChallengeResult>();

            _photoService.Verify(
                service => service.UploadAsync(
                    It.IsAny<ApplicationUser>(),
                    It.IsAny<IFormFile>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>()),
                Times.Never);
        }

        [Fact]
        public async Task Upload_post_with_valid_request_uploads_logs_and_redirects_index()
        {
            
            var user = CreateUser(
                id: "user-1",
                email: "user@example.test");

            SetCurrentUser(authenticated: true);

            var file = CreateFormFile(
                "photo.jpg",
                "image/jpeg",
                new byte[] { 1, 2, 3, 4 });

            _userManager
                .Setup(manager => manager.GetUserAsync(
                    It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            _photoService
                .Setup(service => service.UploadAsync(
                    user,
                    file,
                    "A photo",
                    "#summer",
                    "Grayscale"))
                .ReturnsAsync(CreatePhoto(user.Id));

      
            var result = await _controller.Upload(
                file,
                "A photo",
                "#summer",
                "Grayscale");

            _photoService.Verify(
                service => service.UploadAsync(
                    user,
                    file,
                    "A photo",
                    "#summer",
                    "Grayscale"),
                Times.Once);

            _actionLogger.Verify(
                logger => logger.LogAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    "Uploaded photo 'photo.jpg' (processing: Grayscale)"),
                Times.Once);

            var redirect = result
                .Should()
                .BeOfType<RedirectToActionResult>()
                .Subject;

            redirect.ActionName.Should().Be("Index");
        }

        [Fact]
        public async Task Upload_post_when_pending_package_is_effective_activates_package_before_upload()
        {
            var user = CreateUser(
                id: "user-1",
                email: "user@example.test",
                package: PackageType.Free);

            user.PendingPackage = PackageType.Gold;
            user.PendingPackageEffectiveDate = DateTime.UtcNow.Date;

            SetCurrentUser(authenticated: true);

            var file = CreateFormFile(
                "photo.jpg",
                "image/jpeg",
                new byte[] { 1, 2, 3 });

            _userManager
                .Setup(manager => manager.GetUserAsync(
                    It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            _userManager
                .Setup(manager => manager.UpdateAsync(user))
                .ReturnsAsync(IdentityResult.Success);

            _photoService
                .Setup(service => service.UploadAsync(
                    user,
                    file,
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>()))
                .ReturnsAsync(CreatePhoto(user.Id));

        
            await _controller.Upload(
                file,
                null,
                null,
                "Original");

            user.Package.Should().Be(PackageType.Gold);
            user.PendingPackage.Should().BeNull();
            user.PendingPackageEffectiveDate.Should().BeNull();

            _userManager.Verify(
                manager => manager.UpdateAsync(user),
                Times.Once);
        }

        [Fact]
        public async Task Details_when_photo_does_not_exist_returns_not_found()
        {
           
            var id = Guid.NewGuid();

            _photoService
                .Setup(service => service.GetByIdAsync(id))
                .ReturnsAsync((Photo?)null);

       
            var result = await _controller.Details(id);

          
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Details_when_photo_exists_returns_view_with_photo()
        {
           
            var photo = CreatePhoto("user-1");

            _photoService
                .Setup(service => service.GetByIdAsync(photo.Id))
                .ReturnsAsync(photo);

      
            var result = await _controller.Details(photo.Id);

            var view = result.Should().BeOfType<ViewResult>().Subject;

            view.Model.Should().BeSameAs(photo);
        }

        [Fact]
        public async Task Search_calls_service_with_filters_sets_view_data_and_returns_results()
        {
            
            var from = new DateTime(2026, 9, 1);
            var to = new DateTime(2026, 9, 4);

            var results = new[]
            {
                CreatePhoto("user-1")
            };

            _photoService
                .Setup(service => service.SearchAsync(
                    "nature",
                    100L,
                    1_000L,
                    from,
                    to,
                    "anela"))
                .ReturnsAsync(results);

        
            var result = await _controller.Search(
                "nature",
                100,
                1_000,
                from,
                to,
                "anela");

     
            var view = result.Should().BeOfType<ViewResult>().Subject;

            view.Model.Should().BeSameAs(results);

            ((string?)_controller.ViewData["Hashtag"])
                .Should()
                .Be("nature");

            ((long?)_controller.ViewData["MinSize"])
                .Should()
                .Be(100);

            ((long?)_controller.ViewData["MaxSize"])
                .Should()
                .Be(1_000);

            ((string?)_controller.ViewData["From"])
                .Should()
                .Be("2026-09-01");

            ((string?)_controller.ViewData["To"])
                .Should()
                .Be("2026-09-04");

            ((string?)_controller.ViewData["Author"])
                .Should()
                .Be("anela");
        }

        [Fact]
        public async Task Download_when_file_exists_returns_file_content_result()
        {
            
            var photo = CreatePhoto(
                userId: "user-1",
                fileName: "photo.jpg",
                contentType: "image/png");

            var bytes = new byte[] { 10, 20, 30 };

            _photoService
                .Setup(service => service.GetFileAsync(photo.Id))
                .ReturnsAsync((photo, bytes));

        
            var result = await _controller.Download(photo.Id);

     
            var file = result
                .Should()
                .BeOfType<FileContentResult>()
                .Subject;

            file.FileContents.Should().Equal(bytes);
            file.ContentType.Should().Be("image/png");
            file.FileDownloadName.Should().Be("photo.jpg");
        }

        [Fact]
        public async Task Download_when_photo_file_is_missing_returns_not_found()
        {
          
            var id = Guid.NewGuid();

            _photoService
                .Setup(service => service.GetFileAsync(id))
                .ThrowsAsync(new FileNotFoundException());

         
            var result = await _controller.Download(id);

        
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Download_when_photo_has_null_metadata_uses_safe_defaults()
        {
          
            var photo = CreatePhoto(
                userId: "user-1",
                fileName: null,
                contentType: null);

            var bytes = new byte[] { 10, 20, 30 };

            _photoService
                .Setup(service => service.GetFileAsync(photo.Id))
                .ReturnsAsync((photo, bytes));

            var result = await _controller.Download(photo.Id);

           
            var file = result
                .Should()
                .BeOfType<FileContentResult>()
                .Subject;

            file.ContentType.Should().Be("application/octet-stream");
            file.FileDownloadName.Should().Be("photo");
        }

        [Fact]
        public async Task DownloadProcessed_when_processing_succeeds_returns_jpeg_file_with_option_name()
        {
          
            var photo = CreatePhoto(
                userId: "user-1",
                fileName: "holiday.photo.jpg");

            var bytes = new byte[] { 10, 20, 30 };

            _photoService
                .Setup(service => service.GetProcessedFileAsync(
                    photo.Id,
                    "Grayscale"))
                .ReturnsAsync((photo, bytes));

       
            var result = await _controller.DownloadProcessed(
                photo.Id,
                "Grayscale");

          
            var file = result
                .Should()
                .BeOfType<FileContentResult>()
                .Subject;

            file.FileContents.Should().Equal(bytes);
            file.ContentType.Should().Be("image/jpeg");
            file.FileDownloadName.Should()
                .Be("holiday.photo_Grayscale.jpg");
        }

        [Fact]
        public async Task DownloadProcessed_when_photo_is_missing_returns_not_found()
        {
            
            var id = Guid.NewGuid();

            _photoService
                .Setup(service => service.GetProcessedFileAsync(
                    id,
                    "Original"))
                .ThrowsAsync(new FileNotFoundException());

        
            var result = await _controller.DownloadProcessed(
                id,
                "Original");

          
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task DownloadProcessed_when_unexpected_error_occurs_returns_500()
        {
           
            var id = Guid.NewGuid();

            _photoService
                .Setup(service => service.GetProcessedFileAsync(
                    id,
                    "Original"))
                .ThrowsAsync(new InvalidOperationException("Processing failed."));

     
            var result = await _controller.DownloadProcessed(
                id,
                "Original");

    
            var objectResult = result
                .Should()
                .BeOfType<ObjectResult>()
                .Subject;

            objectResult.StatusCode.Should().Be(
                StatusCodes.Status500InternalServerError);

            objectResult.Value.Should().Be(
                "An error occurred while downloading the processed photo.");
        }

        [Fact]
        public async Task Edit_get_when_photo_does_not_exist_returns_not_found()
        {
          
            var user = CreateUser("user-1", "user@example.test");

            _userManager
                .Setup(manager => manager.GetUserAsync(
                    It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

        
            var result = await _controller.Edit(Guid.NewGuid());

     
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_get_when_photo_belongs_to_another_user_returns_forbid()
        {
    
            var currentUser = CreateUser("current-user", "current@example.test");

            var photo = CreatePhoto("owner-user");

            _context.Photos.Add(photo);
            await _context.SaveChangesAsync();

            _userManager
                .Setup(manager => manager.GetUserAsync(
                    It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(currentUser);

        
            var result = await _controller.Edit(photo.Id);

      
            result.Should().BeOfType<ForbidResult>();
        }

        [Fact]
        public async Task Edit_get_when_current_user_owns_photo_returns_edit_view_model()
        {
           
            var user = CreateUser("user-1", "user@example.test");

            var photo = CreatePhoto(
                userId: user.Id,
                description: "Original description",
                hashtags: "old,tags");

            _context.Photos.Add(photo);
            await _context.SaveChangesAsync();

            _userManager
                .Setup(manager => manager.GetUserAsync(
                    It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

        
            var result = await _controller.Edit(photo.Id);

      
            var view = result.Should().BeOfType<ViewResult>().Subject;

            var model = view.Model
                .Should()
                .BeOfType<PhotoEditViewModel>()
                .Subject;

            model.Id.Should().Be(photo.Id);
            model.Description.Should().Be("Original description");
            model.Hashtags.Should().Be("old,tags");
        }

        [Fact]
        public async Task Edit_post_when_model_state_is_invalid_returns_same_view_model()
        {
         
            var model = new PhotoEditViewModel
            {
                Id = Guid.NewGuid(),
                Description = "Description",
                Hashtags = "tag"
            };

            _controller.ModelState.AddModelError(
                "Description",
                "Description is invalid.");

        
            var result = await _controller.Edit(model);

       
            var view = result.Should().BeOfType<ViewResult>().Subject;

            view.Model.Should().BeSameAs(model);
        }

        [Fact]
        public async Task Edit_post_when_photo_does_not_exist_returns_not_found()
        {
           
            var user = CreateUser("user-1", "user@example.test");

            var model = new PhotoEditViewModel
            {
                Id = Guid.NewGuid(),
                Description = "Updated",
                Hashtags = "updated"
            };

            _userManager
                .Setup(manager => manager.GetUserAsync(
                    It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

          
            var result = await _controller.Edit(model);

         
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task Edit_post_when_photo_belongs_to_another_user_returns_forbid()
        {
            
            var currentUser = CreateUser(
                "current-user",
                "current@example.test");

            var photo = CreatePhoto("owner-user");

            _context.Photos.Add(photo);
            await _context.SaveChangesAsync();

            var model = new PhotoEditViewModel
            {
                Id = photo.Id,
                Description = "Attempted update",
                Hashtags = "attempt"
            };

            _userManager
                .Setup(manager => manager.GetUserAsync(
                    It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(currentUser);

          
            var result = await _controller.Edit(model);

         
            result.Should().BeOfType<ForbidResult>();

            var unchanged = await _context.Photos.FindAsync(photo.Id);

            unchanged!.Description.Should().Be(photo.Description);
            unchanged.Hashtags.Should().Be(photo.Hashtags);

            _actionLogger.Verify(
                logger => logger.LogAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task Edit_post_when_current_user_owns_photo_updates_logs_and_redirects_details()
        {
           
            var user = CreateUser("user-1", "user@example.test");

            var photo = CreatePhoto(
                userId: user.Id,
                description: "Old description",
                hashtags: "old");

            _context.Photos.Add(photo);
            await _context.SaveChangesAsync();

            var model = new PhotoEditViewModel
            {
                Id = photo.Id,
                Description = "New description",
                Hashtags = "new,tags"
            };

            _userManager
                .Setup(manager => manager.GetUserAsync(
                    It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

          
            var result = await _controller.Edit(model);

            var updated = await _context.Photos.FindAsync(photo.Id);

            updated!.Description.Should().Be("New description");
            updated.Hashtags.Should().Be("new,tags");

            _actionLogger.Verify(
                logger => logger.LogAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    $"Edited photo {photo.Id} (description/hashtags)"),
                Times.Once);

            var redirect = result
                .Should()
                .BeOfType<RedirectToActionResult>()
                .Subject;

            redirect.ActionName.Should().Be("Details");
            redirect.RouteValues!["id"].Should().Be(photo.Id);
        }
        [Fact]
        public async Task Search_when_all_filters_are_null_sets_null_view_data_and_returns_results()
        {
         
            var results = new List<Photo>
    {
        new Photo
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            FileName = "photo.jpg",
            StoragePath = "uploads/photo.jpg",
            SizeBytes = 100,
            UploadedAt = DateTime.UtcNow
        }
    };

            _photoService
                .Setup(service => service.SearchAsync(
                    null,
                    null,
                    null,
                    null,
                    null,
                    null))
                .ReturnsAsync(results);

          
            var result = await _controller.Search(
                hashtag: null,
                minSize: null,
                maxSize: null,
                from: null,
                to: null,
                author: null);

        
            var view = result.Should()
                .BeOfType<ViewResult>()
                .Subject;

            view.Model.Should().BeSameAs(results);

            _controller.ViewData["Hashtag"].Should().BeNull();
            _controller.ViewData["MinSize"].Should().BeNull();
            _controller.ViewData["MaxSize"].Should().BeNull();
            _controller.ViewData["From"].Should().BeNull();
            _controller.ViewData["To"].Should().BeNull();
            _controller.ViewData["Author"].Should().BeNull();

            _photoService.Verify(
                service => service.SearchAsync(
                    null,
                    null,
                    null,
                    null,
                    null,
                    null),
                Times.Once);
        }
        [Fact]
        public async Task Search_formats_from_and_to_dates_as_iso_date_only_strings()
        {
          
            var from = new DateTime(2026, 1, 2, 18, 45, 30);
            var to = new DateTime(2026, 12, 31, 23, 59, 59);

            _photoService
                .Setup(service => service.SearchAsync(
                    null,
                    null,
                    null,
                    from,
                    to,
                    null))
                .ReturnsAsync(new List<Photo>());

         
            await _controller.Search(
                hashtag: null,
                minSize: null,
                maxSize: null,
                from: from,
                to: to,
                author: null);

         
            ((string?)_controller.ViewData["From"])
                .Should()
                .Be("2026-01-02");

            ((string?)_controller.ViewData["To"])
                .Should()
                .Be("2026-12-31");
        }
        private void SetCurrentUser(bool authenticated)
        {
            var identity = authenticated
                ? new ClaimsIdentity(
                    new[]
                    {
                        new Claim(
                            ClaimTypes.NameIdentifier,
                            "current-user")
                    },
                    "TestAuthentication")
                : new ClaimsIdentity();

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            };

            _controller.TempData = new TempDataDictionary(
                _controller.HttpContext,
                Mock.Of<ITempDataProvider>());
        }

        private static IFormFile CreateFormFile(
            string fileName,
            string contentType,
            byte[] bytes)
        {
            var stream = new MemoryStream(bytes);

            return new FormFile(
                stream,
                0,
                stream.Length,
                "photoFile",
                fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }

        private static ApplicationUser CreateUser(
            string id,
            string email,
            PackageType package = PackageType.Free)
        {
            return new ApplicationUser
            {
                Id = id,
                UserName = email,
                Email = email,
                Package = package
            };
        }

        private static Photo CreatePhoto(
            string userId,
            string? fileName = "photo.jpg",
            string? contentType = "image/jpeg",
            string? description = null,
            string? hashtags = null)
        {
            return new Photo
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                FileName = fileName!,
                StoragePath = $"uploads/{fileName ?? "photo.jpg"}",
                ContentType = contentType,
                Description = description,
                Hashtags = hashtags,
                SizeBytes = 100,
                UploadedAt = DateTime.UtcNow
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