namespace Obrasci.Models
{
    public class UserActionLog
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public string? UserEmail { get; set; }
        public DateTime Timestamp { get; set; }
        public string Action { get; set; } = string.Empty;
    }
}
