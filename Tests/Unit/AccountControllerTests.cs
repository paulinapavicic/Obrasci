using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Obrasci.Controllers;
using Obrasci.Data;
using Obrasci.Models;
using Obrasci.ViewModels;
using System.Security.Claims;
using Xunit;

namespace Tests.Unit
{
    public class AccountControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<UserManager<ApplicationUser>> _userManager;
        private readonly Mock<SignInManager<ApplicationUser>> _signInManager;
        private readonly Mock<IUrlHelper> _urlHelper = new();
        private readonly AccountController _controller;

        public AccountControllerTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);

            _userManager = CreateUserManager();
            _signInManager = CreateSignInManager(_userManager.Object);

            _controller = new AccountController(
                _signInManager.Object,
                _userManager.Object,
                _context);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            _controller.TempData = new TempDataDictionary(
                _controller.HttpContext,
                Mock.Of<ITempDataProvider>());

            _controller.Url = _urlHelper.Object;
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        [Fact]
        public void Login_get_returns_view_and_stores_return_url()
        {
            
            var result = _controller.Login("/Photos");

            
            result.Should().BeOfType<ViewResult>();

            ((string?)_controller.ViewData["ReturnUrl"])
                .Should()
                .Be("/Photos");
        }

        [Fact]
        public async Task Login_post_when_user_does_not_exist_returns_view_with_model_error()
        {
            _userManager
                .Setup(manager => manager.FindByEmailAsync("missing@example.test"))
                .ReturnsAsync((ApplicationUser?)null);

            
            var result = await _controller.Login(
                "missing@example.test",
                "Password123!",
                "/Photos");

            
            result.Should().BeOfType<ViewResult>();

            _controller.ModelState.IsValid.Should().BeFalse();

            _controller.ModelState[string.Empty]!.Errors
                .Should()
                .ContainSingle(error =>
                    error.ErrorMessage == "Invalid login attempt.");

            _signInManager.Verify(
                manager => manager.PasswordSignInAsync(
                    It.IsAny<ApplicationUser>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>()),
                Times.Never);
        }

        [Fact]
        public async Task Login_post_when_password_is_invalid_returns_view_with_model_error()
        {
           
            var user = CreateUser();

            _userManager
                .Setup(manager => manager.FindByEmailAsync(user.Email!))
                .ReturnsAsync(user);

            _signInManager
                .Setup(manager => manager.PasswordSignInAsync(
                    user,
                    "wrong-password",
                    true,
                    false))
                .ReturnsAsync(
                    Microsoft.AspNetCore.Identity.SignInResult.Failed);

           
            var result = await _controller.Login(
                user.Email!,
                "wrong-password");

           
            result.Should().BeOfType<ViewResult>();

            _controller.ModelState[string.Empty]!.Errors
                .Should()
                .ContainSingle(error =>
                    error.ErrorMessage == "Invalid login attempt.");
        }

        [Fact]
        public async Task Login_post_when_login_succeeds_with_local_return_url_redirects_to_local_url()
        {
           
            var user = CreateUser();

            _userManager
                .Setup(manager => manager.FindByEmailAsync(user.Email!))
                .ReturnsAsync(user);

            _signInManager
                .Setup(manager => manager.PasswordSignInAsync(
                    user,
                    "Password123!",
                    true,
                    false))
                .ReturnsAsync(
                    Microsoft.AspNetCore.Identity.SignInResult.Success);

            _urlHelper
                .Setup(url => url.IsLocalUrl("/Photos"))
                .Returns(true);

            
            var result = await _controller.Login(
                user.Email!,
                "Password123!",
                "/Photos");

            
            var redirect = result.Should()
                .BeOfType<RedirectResult>()
                .Subject;

            redirect.Url.Should().Be("/Photos");

            _urlHelper.Verify(
                url => url.IsLocalUrl("/Photos"),
                Times.Once);
        }

        [Fact]
        public async Task Login_post_when_login_succeeds_with_external_return_url_redirects_home()
        {
            
            var user = CreateUser();

            _userManager
                .Setup(manager => manager.FindByEmailAsync(user.Email!))
                .ReturnsAsync(user);

            _signInManager
                .Setup(manager => manager.PasswordSignInAsync(
                    user,
                    "Password123!",
                    true,
                    false))
                .ReturnsAsync(
                    Microsoft.AspNetCore.Identity.SignInResult.Success);

            _urlHelper
                .Setup(url => url.IsLocalUrl("https://evil.example"))
                .Returns(false);

          
            var result = await _controller.Login(
                user.Email!,
                "Password123!",
                "https://evil.example");

            var redirect = result.Should()
                .BeOfType<RedirectToActionResult>()
                .Subject;

            redirect.ActionName.Should().Be("Index");
            redirect.ControllerName.Should().Be("Home");
        }

        [Fact]
        public async Task Login_post_when_login_succeeds_without_return_url_redirects_home()
        {
            
            var user = CreateUser();

            _userManager
                .Setup(manager => manager.FindByEmailAsync(user.Email!))
                .ReturnsAsync(user);

            _signInManager
                .Setup(manager => manager.PasswordSignInAsync(
                    user,
                    "Password123!",
                    true,
                    false))
                .ReturnsAsync(
                    Microsoft.AspNetCore.Identity.SignInResult.Success);

            
            var result = await _controller.Login(
                user.Email!,
                "Password123!",
                null);

           
            var redirect = result.Should()
                .BeOfType<RedirectToActionResult>()
                .Subject;

            redirect.ActionName.Should().Be("Index");
            redirect.ControllerName.Should().Be("Home");

            _urlHelper.Verify(
                url => url.IsLocalUrl(It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task Logout_signs_out_and_redirects_home()
        {
            
            var result = await _controller.Logout();

            
            _signInManager.Verify(
                manager => manager.SignOutAsync(),
                Times.Once);

            var redirect = result.Should()
                .BeOfType<RedirectToActionResult>()
                .Subject;

            redirect.ActionName.Should().Be("Index");
            redirect.ControllerName.Should().Be("Home");
        }

        [Fact]
        public void ExternalLogin_configures_provider_and_returns_challenge()
        {
            
            var properties = new AuthenticationProperties();

            _urlHelper
                .Setup(url => url.Action(It.IsAny<UrlActionContext>()))
                .Returns("/Account/ExternalLoginCallback");

            _signInManager
                .Setup(manager => manager.ConfigureExternalAuthenticationProperties(
                    "Google",
                    "/Account/ExternalLoginCallback",
                    null))
                .Returns(properties);

            
            var result = _controller.ExternalLogin("Google", "/Photos");

            var challenge = result.Should()
                .BeOfType<ChallengeResult>()
                .Subject;

            challenge.AuthenticationSchemes
                .Should()
                .ContainSingle()
                .Which
                .Should()
                .Be("Google");

            challenge.Properties.Should().BeSameAs(properties);

            _signInManager.Verify(
                manager => manager.ConfigureExternalAuthenticationProperties(
                    "Google",
                    "/Account/ExternalLoginCallback",
                    null),
                Times.Once);
        }

        [Fact]
        public async Task ExternalLoginCallback_when_remote_error_redirects_to_login_and_adds_model_error()
        {
            
            var result = await _controller.ExternalLoginCallback(
                returnUrl: "/Photos",
                remoteError: "access_denied");

            
            var redirect = result.Should()
                .BeOfType<RedirectToActionResult>()
                .Subject;

            redirect.ActionName.Should().Be(nameof(AccountController.Login));

            _controller.ModelState[string.Empty]!.Errors
                .Should()
                .ContainSingle(error =>
                    error.ErrorMessage ==
                    "External provider error: access_denied");

            _signInManager.Verify(
                manager => manager.GetExternalLoginInfoAsync(null),
                Times.Never);
        }

        [Fact]
        public async Task ExternalLoginCallback_when_external_login_info_is_missing_redirects_to_login()
        {
            
            _signInManager
                .Setup(manager => manager.GetExternalLoginInfoAsync(null))
                .ReturnsAsync((ExternalLoginInfo?)null);

           
            var result = await _controller.ExternalLoginCallback("/Photos");

           
            var redirect = result.Should()
                .BeOfType<RedirectToActionResult>()
                .Subject;

            redirect.ActionName.Should().Be(nameof(AccountController.Login));
        }

        [Fact]
        public async Task ExternalLoginCallback_when_existing_external_login_succeeds_redirects_to_local_return_url()
        {
            
            var info = CreateExternalLoginInfo(
                provider: "Google",
                providerKey: "google-123",
                email: "google-user@example.test");

            _signInManager
                .Setup(manager => manager.GetExternalLoginInfoAsync(null))
                .ReturnsAsync(info);

            _signInManager
                .Setup(manager => manager.ExternalLoginSignInAsync(
                    "Google",
                    "google-123",
                    true))
                .ReturnsAsync(
                    Microsoft.AspNetCore.Identity.SignInResult.Success);

           
            var result = await _controller.ExternalLoginCallback("/Photos");

           
            var redirect = result.Should()
                .BeOfType<LocalRedirectResult>()
                .Subject;

            redirect.Url.Should().Be("/Photos");

            _signInManager.Verify(
                manager => manager.ExternalLoginSignInAsync(
                    "Google",
                    "google-123",
                    true),
                Times.Once);
        }

        [Fact]
        public async Task ExternalLoginCallback_when_external_sign_in_fails_returns_external_register_view()
        {
           
            var info = CreateExternalLoginInfo(
                provider: "GitHub",
                providerKey: "github-456",
                email: "github-user@example.test");

            _signInManager
                .Setup(manager => manager.GetExternalLoginInfoAsync(null))
                .ReturnsAsync(info);

            _signInManager
                .Setup(manager => manager.ExternalLoginSignInAsync(
                    "GitHub",
                    "github-456",
                    true))
                .ReturnsAsync(
                    Microsoft.AspNetCore.Identity.SignInResult.Failed);

            
            var result = await _controller.ExternalLoginCallback("/Photos");

           
            var view = result.Should()
                .BeOfType<ViewResult>()
                .Subject;

            view.ViewName.Should().Be("ExternalRegister");

            var model = view.Model
                .Should()
                .BeOfType<ExternalRegisterViewModel>()
                .Subject;

            model.Email.Should().Be("github-user@example.test");
            model.Package.Should().Be(PackageType.Free);
            model.ReturnUrl.Should().Be("/Photos");
            model.LoginProvider.Should().Be("GitHub");
        }

        [Fact]
        public async Task ExternalLoginCallback_when_provider_has_no_email_uses_fallback_email()
        {
           
            var info = CreateExternalLoginInfo(
                provider: "GitHub",
                providerKey: "github-456",
                email: null);

            _signInManager
                .Setup(manager => manager.GetExternalLoginInfoAsync(null))
                .ReturnsAsync(info);

            _signInManager
                .Setup(manager => manager.ExternalLoginSignInAsync(
                    "GitHub",
                    "github-456",
                    true))
                .ReturnsAsync(
                    Microsoft.AspNetCore.Identity.SignInResult.Failed);

           
            var result = await _controller.ExternalLoginCallback("/Photos");

           
            var view = result.Should()
                .BeOfType<ViewResult>()
                .Subject;

            var model = view.Model
                .Should()
                .BeOfType<ExternalRegisterViewModel>()
                .Subject;

            model.Email.Should().Be("github-456@GitHub.local");
        }

        [Fact]
        public async Task ExternalLoginCallback_when_return_url_is_null_redirects_to_application_root()
        {
            
            var info = CreateExternalLoginInfo(
                provider: "Google",
                providerKey: "google-123",
                email: "google-user@example.test");

            _urlHelper
                .Setup(url => url.Content("~/"))
                .Returns("/");

            _signInManager
                .Setup(manager => manager.GetExternalLoginInfoAsync(null))
                .ReturnsAsync(info);

            _signInManager
                .Setup(manager => manager.ExternalLoginSignInAsync(
                    "Google",
                    "google-123",
                    true))
                .ReturnsAsync(
                    Microsoft.AspNetCore.Identity.SignInResult.Success);

          
            var result = await _controller.ExternalLoginCallback();

            
            var redirect = result.Should()
                .BeOfType<LocalRedirectResult>()
                .Subject;

            redirect.Url.Should().Be("/");
        }

        [Fact]
        public async Task ExternalRegister_when_model_state_is_invalid_returns_same_view_model()
        {
            
            var model = CreateExternalRegisterModel();

            _controller.ModelState.AddModelError(
                nameof(ExternalRegisterViewModel.Email),
                "Email is required.");

            
            var result = await _controller.ExternalRegister(model);

            
            var view = result.Should()
                .BeOfType<ViewResult>()
                .Subject;

            view.Model.Should().BeSameAs(model);

            _signInManager.Verify(
                manager => manager.GetExternalLoginInfoAsync(null),
                Times.Never);
        }

        [Fact]
        public async Task ExternalRegister_when_external_login_info_is_missing_redirects_to_login()
        {
          
            var model = CreateExternalRegisterModel();

            _signInManager
                .Setup(manager => manager.GetExternalLoginInfoAsync(null))
                .ReturnsAsync((ExternalLoginInfo?)null);

            
            var result = await _controller.ExternalRegister(model);

            
            var redirect = result.Should()
                .BeOfType<RedirectToActionResult>()
                .Subject;

            redirect.ActionName.Should().Be(nameof(AccountController.Login));
        }

        [Fact]
        public async Task ExternalRegister_when_new_user_creation_fails_returns_view_and_adds_errors()
        {
            
            var model = CreateExternalRegisterModel();

            var info = CreateExternalLoginInfo(
                provider: "Google",
                providerKey: "google-123",
                email: model.Email);

            _signInManager
                .Setup(manager => manager.GetExternalLoginInfoAsync(null))
                .ReturnsAsync(info);

            _userManager
                .Setup(manager => manager.FindByEmailAsync(model.Email))
                .ReturnsAsync((ApplicationUser?)null);

            _userManager
                .Setup(manager => manager.CreateAsync(
                    It.IsAny<ApplicationUser>()))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "DuplicateEmail",
                        Description = "Could not create external user."
                    }));

          
            var result = await _controller.ExternalRegister(model);

          
            var view = result.Should()
                .BeOfType<ViewResult>()
                .Subject;

            view.Model.Should().BeSameAs(model);

            _controller.ModelState[string.Empty]!.Errors
                .Should()
                .ContainSingle(error =>
                    error.ErrorMessage == "Could not create external user.");

            _userManager.Verify(
                manager => manager.AddLoginAsync(
                    It.IsAny<ApplicationUser>(),
                    It.IsAny<UserLoginInfo>()),
                Times.Never);

            _signInManager.Verify(
                manager => manager.SignInAsync(
                    It.IsAny<ApplicationUser>(),
                    true,
                    null),
                Times.Never);
        }

        [Fact]
        public async Task ExternalRegister_when_add_login_fails_returns_view_and_adds_errors()
        {
           
            var model = CreateExternalRegisterModel();

            var info = CreateExternalLoginInfo(
                provider: "Google",
                providerKey: "google-123",
                email: model.Email);

            _signInManager
                .Setup(manager => manager.GetExternalLoginInfoAsync(null))
                .ReturnsAsync(info);

            _userManager
                .Setup(manager => manager.FindByEmailAsync(model.Email))
                .ReturnsAsync((ApplicationUser?)null);

            _userManager
                .Setup(manager => manager.CreateAsync(
                    It.IsAny<ApplicationUser>()))
                .ReturnsAsync(IdentityResult.Success);

            _userManager
                .Setup(manager => manager.AddLoginAsync(
                    It.IsAny<ApplicationUser>(),
                    info))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "ExternalLoginAlreadyAssociated",
                        Description = "External login could not be linked."
                    }));

          
            var result = await _controller.ExternalRegister(model);

          
            var view = result.Should()
                .BeOfType<ViewResult>()
                .Subject;

            view.Model.Should().BeSameAs(model);

            _controller.ModelState[string.Empty]!.Errors
                .Should()
                .ContainSingle(error =>
                    error.ErrorMessage ==
                    "External login could not be linked.");

            _signInManager.Verify(
                manager => manager.SignInAsync(
                    It.IsAny<ApplicationUser>(),
                    true,
                    null),
                Times.Never);
        }

        [Fact]
        public async Task ExternalRegister_when_user_exists_links_login_signs_in_and_redirects()
        {
            var model = CreateExternalRegisterModel(
                email: "existing@example.test",
                returnUrl: "/Photos");

            var existingUser = CreateUser();
            existingUser.Email = model.Email;
            existingUser.UserName = model.Email;

            var info = CreateExternalLoginInfo(
                provider: "Google",
                providerKey: "google-existing-123",
                email: model.Email);

            _signInManager
                .Setup(manager => manager.GetExternalLoginInfoAsync(null))
                .ReturnsAsync(info);

            _userManager
                .Setup(manager => manager.FindByEmailAsync(model.Email))
                .ReturnsAsync(existingUser);

            _userManager
                .Setup(manager => manager.AddLoginAsync(existingUser, info))
                .ReturnsAsync(IdentityResult.Success);

            
            var result = await _controller.ExternalRegister(model);

          
            _userManager.Verify(
                manager => manager.CreateAsync(
                    It.IsAny<ApplicationUser>()),
                Times.Never);

            _userManager.Verify(
                manager => manager.AddLoginAsync(existingUser, info),
                Times.Once);

            _signInManager.Verify(
                manager => manager.SignInAsync(
                    existingUser,
                    true,
                    null),
                Times.Once);

            var redirect = result.Should()
                .BeOfType<LocalRedirectResult>()
                .Subject;

            redirect.Url.Should().Be("/Photos");
        }

        [Fact]
        public async Task ExternalRegister_when_new_user_is_created_links_login_signs_in_and_redirects()
        {
            var model = CreateExternalRegisterModel(
                email: "new-external@example.test",
                package: PackageType.Pro,
                returnUrl: "/Photos");

            var info = CreateExternalLoginInfo(
                provider: "Google",
                providerKey: "google-new-123",
                email: model.Email);

            _signInManager
                .Setup(manager => manager.GetExternalLoginInfoAsync(null))
                .ReturnsAsync(info);

            _userManager
                .Setup(manager => manager.FindByEmailAsync(model.Email))
                .ReturnsAsync((ApplicationUser?)null);

            _userManager
                .Setup(manager => manager.CreateAsync(
                    It.IsAny<ApplicationUser>()))
                .ReturnsAsync(IdentityResult.Success);

            _userManager
                .Setup(manager => manager.AddLoginAsync(
                    It.IsAny<ApplicationUser>(),
                    info))
                .ReturnsAsync(IdentityResult.Success);

            
            var result = await _controller.ExternalRegister(model);

            
            _userManager.Verify(
                manager => manager.CreateAsync(
                    It.Is<ApplicationUser>(user =>
                        user.UserName == model.Email &&
                        user.Email == model.Email &&
                        user.Package == model.Package &&
                        user.DailyUploadCount == 0)),
                Times.Once);

            _userManager.Verify(
                manager => manager.AddLoginAsync(
                    It.Is<ApplicationUser>(user =>
                        user.UserName == model.Email &&
                        user.Email == model.Email &&
                        user.Package == model.Package),
                    info),
                Times.Once);

            _signInManager.Verify(
                manager => manager.SignInAsync(
                    It.Is<ApplicationUser>(user =>
                        user.UserName == model.Email &&
                        user.Email == model.Email &&
                        user.Package == model.Package),
                    true,
                    null),
                Times.Once);

            var redirect = result.Should()
                .BeOfType<LocalRedirectResult>()
                .Subject;

            redirect.Url.Should().Be("/Photos");
        }

        [Fact]
        public async Task ExternalRegister_when_return_url_is_null_redirects_to_application_root()
        {
            
            var model = CreateExternalRegisterModel(
                email: "new-external@example.test",
                returnUrl: null);

            var info = CreateExternalLoginInfo(
                provider: "Google",
                providerKey: "google-new-123",
                email: model.Email);

            _urlHelper
                .Setup(url => url.Content("~/"))
                .Returns("/");

            _signInManager
                .Setup(manager => manager.GetExternalLoginInfoAsync(null))
                .ReturnsAsync(info);

            _userManager
                .Setup(manager => manager.FindByEmailAsync(model.Email))
                .ReturnsAsync((ApplicationUser?)null);

            _userManager
                .Setup(manager => manager.CreateAsync(
                    It.IsAny<ApplicationUser>()))
                .ReturnsAsync(IdentityResult.Success);

            _userManager
                .Setup(manager => manager.AddLoginAsync(
                    It.IsAny<ApplicationUser>(),
                    info))
                .ReturnsAsync(IdentityResult.Success);

          
            var result = await _controller.ExternalRegister(model);

            
            var redirect = result.Should()
                .BeOfType<LocalRedirectResult>()
                .Subject;

            redirect.Url.Should().Be("/");
        }

        [Fact]
        public void Register_get_returns_view_with_default_register_view_model()
        {
            
            var result = _controller.Register();

           
            var view = result.Should()
                .BeOfType<ViewResult>()
                .Subject;

            view.Model.Should().BeOfType<RegisterViewModel>();
        }

        [Fact]
        public async Task Register_post_when_model_state_is_invalid_returns_same_view_model()
        {
            var model = CreateRegisterModel();

            _controller.ModelState.AddModelError(
                "Email",
                "Email is required.");

          
            var result = await _controller.Register(model);
            var view = result.Should()
                .BeOfType<ViewResult>()
                .Subject;

            view.Model.Should().BeSameAs(model);

            _userManager.Verify(
                manager => manager.CreateAsync(
                    It.IsAny<ApplicationUser>(),
                    It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task Register_post_when_creation_succeeds_signs_in_sets_success_message_and_redirects_home()
        {
            
            var model = CreateRegisterModel();

            _userManager
                .Setup(manager => manager.CreateAsync(
                    It.IsAny<ApplicationUser>(),
                    model.Password))
                .ReturnsAsync(IdentityResult.Success);

            
            var result = await _controller.Register(model);

           
            _signInManager.Verify(
                manager => manager.SignInAsync(
                    It.Is<ApplicationUser>(user =>
                        user.UserName == model.Email &&
                        user.Email == model.Email &&
                        user.Package == model.Package &&
                        user.DailyUploadCount == 0),
                    true,
                    null),
                Times.Once);

            _controller.TempData["Success"]
                .Should()
                .Be("Registration successful. You are now logged in.");

            var redirect = result.Should()
                .BeOfType<RedirectToActionResult>()
                .Subject;

            redirect.ActionName.Should().Be("Index");
            redirect.ControllerName.Should().Be("Home");
        }

        [Fact]
        public async Task Register_post_when_creation_fails_returns_view_and_adds_identity_errors()
        {
           
            var model = CreateRegisterModel();

            var failure = IdentityResult.Failed(
                new IdentityError
                {
                    Code = "DuplicateEmail",
                    Description = "Email is already registered."
                });

            _userManager
                .Setup(manager => manager.CreateAsync(
                    It.IsAny<ApplicationUser>(),
                    model.Password))
                .ReturnsAsync(failure);

            
            var result = await _controller.Register(model);

            
            var view = result.Should()
                .BeOfType<ViewResult>()
                .Subject;

            view.Model.Should().BeSameAs(model);

            _controller.ModelState[string.Empty]!.Errors
                .Should()
                .ContainSingle(error =>
                    error.ErrorMessage == "Email is already registered.");

            _controller.TempData["Error"]
                .Should()
                .Be("Registration failed. Please fix the errors and try again.");

            _signInManager.Verify(
                manager => manager.SignInAsync(
                    It.IsAny<ApplicationUser>(),
                    It.IsAny<bool>(),
                    It.IsAny<string?>()),
                Times.Never);
        }

        [Fact]
        public async Task ChangePackage_when_no_current_user_returns_challenge()
        {
            _userManager
                .Setup(manager => manager.GetUserAsync(
                    It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync((ApplicationUser?)null);

            
            var result = await _controller.ChangePackage(PackageType.Pro);

            result.Should().BeOfType<ChallengeResult>();
        }

        [Fact]
        public async Task ChangePackage_when_user_already_changed_today_sets_error_and_redirects_usage()
        {
            var user = CreateUser();
            user.PackageLastChanged = DateTime.UtcNow.Date;

            _userManager
                .Setup(manager => manager.GetUserAsync(
                    It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

          
            var result = await _controller.ChangePackage(PackageType.Pro);

            
            _controller.TempData["Error"]
                .Should()
                .Be("You can change package only once per day.");

            var redirect = result.Should()
                .BeOfType<RedirectToActionResult>()
                .Subject;

            redirect.ActionName.Should().Be("Usage");

            _userManager.Verify(
                manager => manager.UpdateAsync(
                    It.IsAny<ApplicationUser>()),
                Times.Never);
        }

        [Fact]
        public async Task ChangePackage_when_user_can_change_schedules_package_for_tomorrow_and_redirects_usage()
        {
            
            var user = CreateUser();
            user.Package = PackageType.Free;
            user.PackageLastChanged = DateTime.UtcNow.AddDays(-1).Date;

            _userManager
                .Setup(manager => manager.GetUserAsync(
                    It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            _userManager
                .Setup(manager => manager.UpdateAsync(user))
                .ReturnsAsync(IdentityResult.Success);

            var today = DateTime.UtcNow.Date;

         
            var result = await _controller.ChangePackage(PackageType.Gold);

            
            user.PendingPackage.Should().Be(PackageType.Gold);
            user.PendingPackageEffectiveDate.Should()
                .Be(today.AddDays(1));

            user.PackageLastChanged.Should().Be(today);

            _userManager.Verify(
                manager => manager.UpdateAsync(user),
                Times.Once);

            _controller.TempData["Message"]
                .Should()
                .Be("Package change scheduled for tomorrow.");

            var redirect = result.Should()
                .BeOfType<RedirectToActionResult>()
                .Subject;

            redirect.ActionName.Should().Be("Usage");
        }

        [Fact]
        public async Task Usage_when_no_current_user_returns_challenge()
        {
            
            _userManager
                .Setup(manager => manager.GetUserAsync(
                    It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync((ApplicationUser?)null);

            
            var result = await _controller.Usage();
            result.Should().BeOfType<ChallengeResult>();
        }

        [Fact]
        public async Task Usage_returns_model_with_photo_totals_and_package_limits()
        {
            
            var user = CreateUser();
            user.Package = PackageType.Pro;
            user.DailyUploadCount = 4;

            _context.Photos.AddRange(
                new Photo
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    FileName = "one.jpg",
                    StoragePath = "uploads/one.jpg",
                    SizeBytes = 100,
                    UploadedAt = DateTime.UtcNow
                },
                new Photo
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    FileName = "two.jpg",
                    StoragePath = "uploads/two.jpg",
                    SizeBytes = 250,
                    UploadedAt = DateTime.UtcNow
                },
                new Photo
                {
                    Id = Guid.NewGuid(),
                    UserId = "another-user",
                    FileName = "other.jpg",
                    StoragePath = "uploads/other.jpg",
                    SizeBytes = 999,
                    UploadedAt = DateTime.UtcNow
                });

            await _context.SaveChangesAsync();

            _userManager
                .Setup(manager => manager.GetUserAsync(
                    It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            _userManager
                .Setup(manager => manager.GetRolesAsync(user))
                .ReturnsAsync(new List<string> { "User" });

            _userManager
                .Setup(manager => manager.IsInRoleAsync(user, "Admin"))
                .ReturnsAsync(false);

            var result = await _controller.Usage();
            
            var view = result.Should()
                .BeOfType<ViewResult>()
                .Subject;

            var model = view.Model
                .Should()
                .BeOfType<UsageViewModel>()
                .Subject;

            model.Package.Should().Be(PackageType.Pro);
            model.DailyUploadCount.Should().Be(4);
            model.DailyUploadLimit.Should().Be(20);
            model.TotalPhotos.Should().Be(2);
            model.TotalSizeBytes.Should().Be(350);
        }

        private static RegisterViewModel CreateRegisterModel()
        {
            return new RegisterViewModel
            {
                Email = "new-user@example.test",
                Password = "Password123!",
                Package = PackageType.Free
            };
        }

        private static ExternalRegisterViewModel CreateExternalRegisterModel(
            string email = "external-user@example.test",
            PackageType package = PackageType.Free,
            string? returnUrl = "/Photos")
        {
            return new ExternalRegisterViewModel
            {
                Email = email,
                Package = package,
                ReturnUrl = returnUrl,
                LoginProvider = "Google"
            };
        }

        private static ApplicationUser CreateUser()
        {
            return new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "account-test-user",
                Email = "account-test@example.test",
                Package = PackageType.Free
            };
        }

        private static ExternalLoginInfo CreateExternalLoginInfo(
            string provider,
            string providerKey,
            string? email)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, providerKey),
                new Claim(ClaimTypes.Name, "External Test User")
            };

            if (email is not null)
            {
                claims.Add(new Claim(ClaimTypes.Email, email));
            }

            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, provider));

            return new ExternalLoginInfo(
                principal,
                provider,
                providerKey,
                $"{provider} External User");
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

        private static Mock<SignInManager<ApplicationUser>> CreateSignInManager(
            UserManager<ApplicationUser> userManager)
        {
            return new Mock<SignInManager<ApplicationUser>>(
                userManager,
                new Mock<IHttpContextAccessor>().Object,
                new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object,
                new Mock<IOptions<IdentityOptions>>().Object,
                new Mock<ILogger<SignInManager<ApplicationUser>>>().Object,
                new Mock<
                    Microsoft.AspNetCore.Authentication
                        .IAuthenticationSchemeProvider>()
                    .Object,
                new Mock<IUserConfirmation<ApplicationUser>>().Object);
        }
    }
}