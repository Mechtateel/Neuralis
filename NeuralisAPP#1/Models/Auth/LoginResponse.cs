namespace NeuralisAPP_1.Models.Auth;

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public bool? IsVerified { get; set; } // ← nullable, И ЭТО ВАЖНО
}
