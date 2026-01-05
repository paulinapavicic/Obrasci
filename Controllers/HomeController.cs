using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Obrasci.Data;
using Obrasci.Models;
using System.Diagnostics;

namespace Obrasci.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

       

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public async Task<IActionResult> Index()
        {
            var latest = await _context.Photos
                .OrderByDescending(p => p.UploadedAt)
                .Take(8)
                .ToListAsync(); 

            ViewBag.LatestPhotos = latest;
            return View();
        }

    }
}
