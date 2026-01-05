using Obrasci.Models;
using System.ComponentModel.DataAnnotations;

namespace Obrasci.ViewModels
{
    public class ExternalRegisterViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public PackageType Package { get; set; } = PackageType.Free;

        public string ReturnUrl { get; set; } = "/";

        public string LoginProvider { get; set; } = string.Empty;
    }
}
