using Obrasci.Data;
using Obrasci.Models;
using System.Security.Claims;

namespace Obrasci.Services
{
    public interface IActionLogger
    {
        Task LogAsync(ClaimsPrincipal user, string action);
    }

    public class ActionLogger : IActionLogger
    {
        private readonly ApplicationDbContext _ctx;

        public ActionLogger(ApplicationDbContext ctx)
        {
            _ctx = ctx;
        }

        public async Task LogAsync(ClaimsPrincipal principal, string action)
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = principal.Identity?.Name;

            var log = new UserActionLog
            {
                UserId = userId,
                UserEmail = email,
                Timestamp = DateTime.UtcNow,
                Action = action
            };

            _ctx.UserActionLogs.Add(log);
            await _ctx.SaveChangesAsync();
        }
    }
}
