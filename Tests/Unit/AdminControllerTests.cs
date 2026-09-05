using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
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
    public class AdminControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<UserManager<ApplicationUser>> _userManager;
        private readonly Mock<IActionLogger> _actionLogger = new();
        private readonly AdminController _controller;

        public AdminControllerTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _userManager = CreateUserManager();

            _controller = new AdminController(
                _context,
                _userManager.Object,
                _actionLogger.Object);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            _controller.TempData = new TempDataDictionary(
                _controller.HttpContext,
                Mock.Of<ITempDataProvider>());
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        [Fact]
        public void AdminController_requires_admin_role()
        {
           
            var attribute = typeof(AdminController)
                .GetCustomAttribute<AuthorizeAttribute>();

          
            attribute.Should().NotBeNull();
            attribute!.Roles.Should().Be("Admin");
        }

        [Fact]
        public async Task Index_returns_view_and_sets_dashboard_counts()
        {
           
            var userOne = CreateUser("user-1", "one@example.test");
            var userTwo = CreateUser("user-2", "two@example.test");

            _context.Users.AddRange(userOne, userTwo);

            _context.Photos.AddRange(
                CreatePhoto(userOne.Id, 100),
                CreatePhoto(userTwo.Id, 200));

            _context.UserActionLogs.AddRange(
                new UserActionLog
                {
                    Action = "First action",
                    Timestamp = DateTime.UtcNow
                },
                new UserActionLog
                {
                    Action = "Second action",
                    Timestamp = DateTime.UtcNow
                },
                new UserActionLog
                {
                    Action = "Third action",
                    Timestamp = DateTime.UtcNow
                });

            await _context.SaveChangesAsync();

           
            var result = await _controller.Index();

            result.Should().BeOfType<ViewResult>();

            ((int)_controller.ViewData["UserCount"]!)
                .Should()
                .Be(2);

            ((int)_controller.ViewData["PhotoCount"]!)
                .Should()
                .Be(2);

            ((int)_controller.ViewData["LogCount"]!)
                .Should()
                .Be(3);
        }

        [Fact]
        public async Task Users_returns_all_users_as_view_model()
        {
           
            var first = CreateUser("user-1", "one@example.test");
            var second = CreateUser("user-2", "two@example.test");

            _context.Users.AddRange(first, second);
            await _context.SaveChangesAsync();

            
            var result = await _controller.Users();

           
            var view = result.Should().BeOfType<ViewResult>().Subject;

            var users = view.Model
                .Should()
                .BeAssignableTo<IEnumerable<ApplicationUser>>()
                .Subject
                .ToList();

            users.Select(user => user.Id)
                .Should()
                .BeEquivalentTo(new[] { first.Id, second.Id });
        }

        [Fact]
        public async Task ChangePackage_when_user_does_not_exist_returns_not_found()
        {
           
            _userManager
                .Setup(manager => manager.FindByIdAsync("missing-user"))
                .ReturnsAsync((ApplicationUser?)null);

           
            var result = await _controller.ChangePackage(
                "missing-user",
                PackageType.Gold);

            
            result.Should().BeOfType<NotFoundResult>();

            _userManager.Verify(
                manager => manager.UpdateAsync(It.IsAny<ApplicationUser>()),
                Times.Never);

            _actionLogger.Verify(
                logger => logger.LogAsync(
                    It.IsAny<System.Security.Claims.ClaimsPrincipal>(),
                    It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task ChangePackage_when_user_exists_updates_package_logs_and_redirects_users()
        {
            var user = CreateUser("user-1", "target@example.test");
            user.Package = PackageType.Free;

            _userManager
                .Setup(manager => manager.FindByIdAsync(user.Id))
                .ReturnsAsync(user);

            _userManager
                .Setup(manager => manager.UpdateAsync(user))
                .ReturnsAsync(IdentityResult.Success);

            
            var result = await _controller.ChangePackage(
                user.Id,
                PackageType.Pro);

            
            user.Package.Should().Be(PackageType.Pro);
            user.PackageLastChanged.Should().BeCloseTo(
                DateTime.UtcNow,
                TimeSpan.FromSeconds(5));

            _userManager.Verify(
                manager => manager.UpdateAsync(user),
                Times.Once);

            _actionLogger.Verify(
                logger => logger.LogAsync(
                    It.IsAny<System.Security.Claims.ClaimsPrincipal>(),
                    $"Changed package of {user.Email} to {PackageType.Pro}"),
                Times.Once);

            var redirect = result
                .Should()
                .BeOfType<RedirectToActionResult>()
                .Subject;

            redirect.ActionName.Should().Be("Users");
        }

        [Fact]
        public async Task Logs_returns_newest_200_logs_first()
        {
            
            var now = DateTime.UtcNow;

            var oldest = new UserActionLog
            {
                Action = "Oldest",
                Timestamp = now.AddMinutes(-3)
            };

            var middle = new UserActionLog
            {
                Action = "Middle",
                Timestamp = now.AddMinutes(-2)
            };

            var newest = new UserActionLog
            {
                Action = "Newest",
                Timestamp = now.AddMinutes(-1)
            };

            _context.UserActionLogs.AddRange(oldest, middle, newest);
            await _context.SaveChangesAsync();

           
            var result = await _controller.Logs();

            
            var view = result.Should().BeOfType<ViewResult>().Subject;

            var logs = view.Model
                .Should()
                .BeAssignableTo<IEnumerable<UserActionLog>>()
                .Subject
                .ToList();

            logs.Select(log => log.Action)
                .Should()
                .Equal("Newest", "Middle", "Oldest");
        }

        [Fact]
        public async Task UserDetails_when_user_does_not_exist_returns_not_found()
        {
            
            _userManager
                .Setup(manager => manager.FindByIdAsync("missing-user"))
                .ReturnsAsync((ApplicationUser?)null);

           
            var result = await _controller.UserDetails("missing-user");

            
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task UserDetails_returns_user_photo_count_and_total_size()
        {
            
            var user = CreateUser("user-1", "details@example.test");

            _context.Photos.AddRange(
                CreatePhoto(user.Id, 100),
                CreatePhoto(user.Id, 250),
                CreatePhoto("other-user", 999));

            await _context.SaveChangesAsync();

            _userManager
                .Setup(manager => manager.FindByIdAsync(user.Id))
                .ReturnsAsync(user);

            
            var result = await _controller.UserDetails(user.Id);

           
            var view = result.Should().BeOfType<ViewResult>().Subject;

            var model = view.Model
                .Should()
                .BeOfType<AdminUserStatsViewModel>()
                .Subject;

            model.User.Should().BeSameAs(user);
            model.TotalPhotos.Should().Be(2);
            model.TotalSizeBytes.Should().Be(350);
        }

        [Fact]
        public async Task Photos_returns_all_photos_newest_first_with_users()
        {
           
            var olderUser = CreateUser("user-1", "older@example.test");
            var newerUser = CreateUser("user-2", "newer@example.test");

            _context.Users.AddRange(olderUser, newerUser);

            var olderPhoto = CreatePhoto(
                olderUser.Id,
                100,
                uploadedAt: DateTime.UtcNow.AddMinutes(-2));

            olderPhoto.User = olderUser;

            var newerPhoto = CreatePhoto(
                newerUser.Id,
                200,
                uploadedAt: DateTime.UtcNow.AddMinutes(-1));

            newerPhoto.User = newerUser;

            _context.Photos.AddRange(olderPhoto, newerPhoto);
            await _context.SaveChangesAsync();

            
            var result = await _controller.Photos();

           
            var view = result.Should().BeOfType<ViewResult>().Subject;

            var photos = view.Model
                .Should()
                .BeAssignableTo<IEnumerable<Photo>>()
                .Subject
                .ToList();

            photos.Select(photo => photo.Id)
                .Should()
                .Equal(newerPhoto.Id, olderPhoto.Id);

            photos.All(photo => photo.User is not null)
    .Should()
    .BeTrue();
        }

        [Fact]
        public async Task DeletePhoto_when_photo_does_not_exist_returns_not_found()
        {
            var result = await _controller.DeletePhoto(Guid.NewGuid());

            
            result.Should().BeOfType<NotFoundResult>();

            _actionLogger.Verify(
                logger => logger.LogAsync(
                    It.IsAny<System.Security.Claims.ClaimsPrincipal>(),
                    It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task DeletePhoto_when_photo_exists_removes_logs_and_redirects_photos()
        {
            
            var photo = CreatePhoto("user-1", 100);

            _context.Photos.Add(photo);
            await _context.SaveChangesAsync();

            var result = await _controller.DeletePhoto(photo.Id);

            
            (await _context.Photos.FindAsync(photo.Id))
                .Should()
                .BeNull();

            _actionLogger.Verify(
                logger => logger.LogAsync(
                    It.IsAny<System.Security.Claims.ClaimsPrincipal>(),
                    $"Deleted photo {photo.Id} of user {photo.UserId}"),
                Times.Once);

            var redirect = result
                .Should()
                .BeOfType<RedirectToActionResult>()
                .Subject;

            redirect.ActionName.Should().Be("Photos");
        }

        [Fact]
        public async Task ChangeEmail_when_user_does_not_exist_returns_not_found()
        {
           
            _userManager
                .Setup(manager => manager.FindByIdAsync("missing-user"))
                .ReturnsAsync((ApplicationUser?)null);

            
            var result = await _controller.ChangeEmail(
                "missing-user",
                "new@example.test");

          
            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task ChangeEmail_when_update_fails_returns_users_view_and_adds_errors()
        {
           
            var user = CreateUser("user-1", "old@example.test");

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _userManager
                .Setup(manager => manager.FindByIdAsync(user.Id))
                .ReturnsAsync(user);

            _userManager
                .Setup(manager => manager.UpdateAsync(user))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "DuplicateEmail",
                        Description = "Email is already used."
                    }));

          
            var result = await _controller.ChangeEmail(
                user.Id,
                "new@example.test");

            
            user.UserName.Should().Be("new@example.test");
            user.Email.Should().Be("new@example.test");

            var view = result.Should().BeOfType<ViewResult>().Subject;

            view.ViewName.Should().Be("Users");

            _controller.ModelState[string.Empty]!.Errors
                .Should()
                .ContainSingle(error =>
                    error.ErrorMessage == "Email is already used.");

            _actionLogger.Verify(
                logger => logger.LogAsync(
                    It.IsAny<System.Security.Claims.ClaimsPrincipal>(),
                    It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task ChangeEmail_when_update_succeeds_logs_and_redirects_users()
        {
            
            var user = CreateUser("user-1", "old@example.test");

            _userManager
                .Setup(manager => manager.FindByIdAsync(user.Id))
                .ReturnsAsync(user);

            _userManager
                .Setup(manager => manager.UpdateAsync(user))
                .ReturnsAsync(IdentityResult.Success);

            
            var result = await _controller.ChangeEmail(
                user.Id,
                "new@example.test");

            user.UserName.Should().Be("new@example.test");
            user.Email.Should().Be("new@example.test");

            _actionLogger.Verify(
                logger => logger.LogAsync(
                    It.IsAny<System.Security.Claims.ClaimsPrincipal>(),
                    $"Changed email of user {user.Id} to new@example.test"),
                Times.Once);

            var redirect = result
                .Should()
                .BeOfType<RedirectToActionResult>()
                .Subject;

            redirect.ActionName.Should().Be("Users");
        }

        private static ApplicationUser CreateUser(
            string id,
            string email)
        {
            return new ApplicationUser
            {
                Id = id,
                UserName = email,
                Email = email,
                Package = PackageType.Free
            };
        }

        private static Photo CreatePhoto(
            string userId,
            long sizeBytes,
            DateTime? uploadedAt = null)
        {
            return new Photo
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                FileName = "photo.jpg",
                StoragePath = "uploads/photo.jpg",
                SizeBytes = sizeBytes,
                UploadedAt = uploadedAt ?? DateTime.UtcNow,
                ContentType = "image/jpeg"
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