using System.ComponentModel.DataAnnotations;

namespace Obrasci.Dtos;

public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}