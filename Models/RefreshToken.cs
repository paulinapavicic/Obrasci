using System.ComponentModel.DataAnnotations;

namespace Obrasci.Models;

public class RefreshToken
{
    public int Id { get; set; }

    [Required]
    public string TokenHash { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string FamilyId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive =>
        RevokedAtUtc == null && ExpiresAtUtc > DateTime.UtcNow;
}