using Microsoft.Maui.Graphics;

namespace NeuralisAPP_1.Models.Admin;

public class AdminUserModel
{
    public Guid Id { get; set; }

    public string Email { get; set; } = "";

    // 👇 РОЛЬ У ТЕБЯ СТРОКОЙ (Admin / Doctor / User)
    public string Role { get; set; } = "";

    public bool IsBlocked { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool HasDoctorProfile { get; set; }
    public bool? IsDoctorVerified { get; set; }

    // ===== UI HELPERS (НИЧЕГО НЕ ЛОМАЮТ) =====

    public string FullName { get; set; } = "";

    public bool HasFullName =>
        !string.IsNullOrWhiteSpace(FullName);

    public string DisplayName =>
        HasFullName ? FullName : Email;


    // Инициалы для аватарки
    public string Initials =>
        !string.IsNullOrEmpty(Email)
            ? Email.Substring(0, 1).ToUpper()
            : "?";

    // Текст роли (нормальный, а не пустой прямоугольник)
    public string RoleText => string.IsNullOrWhiteSpace(Role)
        ? "User"
        : Role;

    // Цвет роли
    public Color RoleColor => Role switch
    {
        "Admin" => Color.FromArgb("#a855f7"),
        "Doctor" => Color.FromArgb("#38bdf8"),
        "User" => Color.FromArgb("#94a3b8"),
        _ => Colors.Gray
    };
}
