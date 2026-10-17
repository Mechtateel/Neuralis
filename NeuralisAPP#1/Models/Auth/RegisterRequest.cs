namespace NeuralisAPP_1.Models.Auth;

public class RegisterRequest
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Role { get; set; } = "User";
}
