namespace Obrasci.Models
{
    public class ActionLog
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public string? UserEmail { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string Operation { get; set; } = string.Empty;
    }
}
