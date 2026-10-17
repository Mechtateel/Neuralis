using System.ComponentModel.DataAnnotations;

namespace NeuralisAPI.Contracts.Auth
{
    public class LogoutRequest
    {
        [Required]
        public string RefreshToken { get; set; } = null!;
    }
}
