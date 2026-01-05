using Obrasci.Data;
using Obrasci.Models;

namespace Obrasci.Services
{
    public class LoggingService : ILoggingService
    {
        private readonly ApplicationDbContext _ctx;

        public LoggingService(ApplicationDbContext ctx)
        {
            _ctx = ctx;
        }
        public async Task LogAsync(string? userId, string? userEmail, string action)
        {
            var log = new UserActionLog
            {
                UserId = userId,
                UserEmail = userEmail,
                Timestamp = DateTime.UtcNow,
                Action = action
            };

            _ctx.UserActionLogs.Add(log);
            await _ctx.SaveChangesAsync();
        }

      
    }
}
