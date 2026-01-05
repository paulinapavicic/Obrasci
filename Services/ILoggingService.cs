namespace Obrasci.Services
{
    public interface ILoggingService
    {
        Task LogAsync(string? userId, string? userEmail, string action);
    }
}
