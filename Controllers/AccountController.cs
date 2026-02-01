using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Obrasci.Data;
using Obrasci.Models;
using Obrasci.ViewModels;
using System.Security.Claims;

namespace Obrasci.Controllers
{
    public class AccountController : Controller
    {

        //Dependency Injection 
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AccountController(SignInManager<ApplicationUser> signInManager,
                                 UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(user, password, true, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }


        //Adapter- adapts Google/Github into my internal user model 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null)
        {
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            returnUrl ??= Url.Content("~/");

            if (remoteError != null)
            {
                ModelState.AddModelError(string.Empty, $"External provider error: {remoteError}");
                return RedirectToAction(nameof(Login));
            }

            //Adapter- adapts Google/Github into my internal user model 
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
                return RedirectToAction(nameof(Login));

            var signInResult = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey, isPersistent: true);

            if (signInResult.Succeeded)
                return LocalRedirect(returnUrl);

            
            var email = info.Principal.FindFirstValue(ClaimTypes.Email)
                        ?? $"{info.ProviderKey}@{info.LoginProvider}.local";

            var model = new ExternalRegisterViewModel
            {
                Email = email,
                Package = PackageType.Free, 
                ReturnUrl = returnUrl,
                LoginProvider = info.LoginProvider
            };

            return View("ExternalRegister", model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExternalRegister(ExternalRegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            //Adapter- adapts Google/Github into my internal user model 
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
                return RedirectToAction(nameof(Login));

           
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                //Factory Method- creating users
                user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    Package = model.Package,
                    DailyUploadCount = 0
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    foreach (var error in createResult.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);
                    return View(model);
                }
            }

            
            var loginResult = await _userManager.AddLoginAsync(user, info);
            if (!loginResult.Succeeded)
            {
                foreach (var error in loginResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            
            await _signInManager.SignInAsync(user, isPersistent: true);
            return LocalRedirect(model.ReturnUrl ?? Url.Content("~/"));
        }



        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            //Factory Method- creating users
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                Package = model.Package,
                DailyUploadCount = 0
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: true);
                TempData["Success"] = "Registration successful. You are now logged in.";
                return RedirectToAction("Index", "Home");
            }
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
                Console.WriteLine(error.Code + " - " + error.Description);
            }

            TempData["Error"] = "Registration failed. Please fix the errors and try again.";
            return View(model);
        }



        [Authorize]
        public async Task<IActionResult> Usage()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

           
            var roles = await _userManager.GetRolesAsync(user);
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            Console.WriteLine($"User: {user.Email}, Roles: {string.Join(',', roles)}, IsAdmin: {isAdmin}");

            user = await EnsurePackageUpToDate(user);

            // Repository pattern: query Photos via DbSet instead of raw SQL
            var totalPhotos = await _context.Photos
                .CountAsync(p => p.UserId == user.Id);

            var totalSize = await _context.Photos
                .Where(p => p.UserId == user.Id)
                .SumAsync(p => p.SizeBytes);

            //Strategy pattern- rules per package
            var model = new UsageViewModel
            {
                Package = user.Package,
                DailyUploadCount = user.DailyUploadCount,
                DailyUploadLimit = PackageLimits.GetDailyLimit(user.Package),
                TotalPhotos = totalPhotos,
                TotalSizeBytes = totalSize
            };

            return View(model);
        }



        private async Task<ApplicationUser> EnsurePackageUpToDate(ApplicationUser user)
        {
            var today = DateTime.UtcNow.Date;

            if (user.PendingPackage.HasValue &&
                user.PendingPackageEffectiveDate.HasValue &&
                user.PendingPackageEffectiveDate.Value.Date <= today)
            {
                user.Package = user.PendingPackage.Value;
                user.PendingPackage = null;
                user.PendingPackageEffectiveDate = null;

                await _userManager.UpdateAsync(user);
            }

            return user;
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePackage(PackageType newPackage)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var today = DateTime.UtcNow.Date;

            // Only one change per day
            if (user.PackageLastChanged == today)
            {
                TempData["Error"] = "You can change package only once per day.";
                return RedirectToAction("Usage");
            }

            // Schedule change for tomorrow
            user.PendingPackage = newPackage;
            user.PendingPackageEffectiveDate = today.AddDays(1);
            user.PackageLastChanged = today;

            await _userManager.UpdateAsync(user);

            TempData["Message"] = "Package change scheduled for tomorrow.";
            return RedirectToAction("Usage");
        }

    }
}
