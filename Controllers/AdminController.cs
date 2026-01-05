using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Obrasci.Data;
using Obrasci.Models;
using Obrasci.Services;
using Obrasci.ViewModels;

namespace Obrasci.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _ctx;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IActionLogger _actionLogger;

        public AdminController(ApplicationDbContext ctx, UserManager<ApplicationUser> userManager, IActionLogger actionLogger)
        {
            _ctx = ctx;
            _userManager = userManager;
            _actionLogger = actionLogger;
        }

       
        public async Task<IActionResult> Index()
        {
            var userCount = await _ctx.Users.CountAsync();
            var photoCount = await _ctx.Photos.CountAsync();
            var logCount = await _ctx.UserActionLogs.CountAsync();

            ViewBag.UserCount = userCount;
            ViewBag.PhotoCount = photoCount;
            ViewBag.LogCount = logCount;

            return View();
        }

       
        public async Task<IActionResult> Users()
        {
            var users = await _ctx.Users.ToListAsync();
            return View(users);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePackage(string userId, PackageType package)
        {

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            user.Package = package;
            user.PackageLastChanged = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            await _actionLogger.LogAsync(User,
                $"Changed package of {user.Email} to {package}");

            return RedirectToAction(nameof(Users));
        }

       
        public async Task<IActionResult> Logs()
        {
            var logs = await _ctx.UserActionLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(200)
                .ToListAsync();

            return View(logs);
        }

        public async Task<IActionResult> UserDetails(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var totalPhotos = await _ctx.Photos.CountAsync(p => p.UserId == id);
            var totalSize = await _ctx.Photos
                .Where(p => p.UserId == id)
                .SumAsync(p => p.SizeBytes);

            var model = new AdminUserStatsViewModel
            {
                User = user,
                TotalPhotos = totalPhotos,
                TotalSizeBytes = totalSize
            };

            return View(model);
        }


        public async Task<IActionResult> Photos()
        {
            var photos = await _ctx.Photos
                .Include(p => p.User) 
                .OrderByDescending(p => p.UploadedAt)
                .ToListAsync();

            return View(photos);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePhoto(Guid id)
        {
            var photo = await _ctx.Photos.FindAsync(id);
            if (photo == null) return NotFound();

            _ctx.Photos.Remove(photo);

            await _actionLogger.LogAsync(User,
                $"Deleted photo {photo.Id} of user {photo.UserId}");

            await _ctx.SaveChangesAsync();
            return RedirectToAction(nameof(Photos));
        }


      

    }
}
