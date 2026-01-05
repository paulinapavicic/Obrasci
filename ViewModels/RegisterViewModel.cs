using Obrasci.Models;
using System.ComponentModel.DataAnnotations;

namespace Obrasci.ViewModels
{
    public class RegisterViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public PackageType Package { get; set; } = PackageType.Free;
    }
}
