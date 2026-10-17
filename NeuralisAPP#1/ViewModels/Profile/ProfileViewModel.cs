using System.ComponentModel;
using System.Runtime.CompilerServices;
using NeuralisAPP_1.Localization;
using NeuralisAPP_1.Models.Auth;
using NeuralisAPP_1.Models.Doctor;
using NeuralisAPP_1.Models.Profile;
using NeuralisAPP_1.Services;

namespace NeuralisAPP_1.ViewModels.Profile;

public sealed class ProfileViewModel : INotifyPropertyChanged
{
    private readonly ProfilePreferencesService _prefsService = ProfilePreferencesService.Instance;

    private ProfilePreferences _prefs = new();
    private MeResponse? _user;

    public ProfileViewModel()
    {
        Reload();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // ===== Identity (server) =====
    public MeResponse? User => _user;
    public Guid UserId => _user?.Id ?? Guid.Empty;
    public UserRole Role => _user == null ? UserRole.User : (UserRole)_user.Role;
    public bool IsLabTechnician => DoctorSpecializationUi.IsLabTechnician(_user?.DoctorSpecialization);
    public bool IsVerifiedDoctor => Role == UserRole.Doctor && (_user?.IsVerified ?? false);

    public string DisplayName =>
        string.IsNullOrWhiteSpace(_user?.FullName)
            ? (_user?.Email ?? "User")
            : _user!.FullName!;

    public string Email => string.IsNullOrWhiteSpace(_user?.Email) ? "—" : _user!.Email!;

    public string Specialization =>
        DoctorSpecializationUi.Format(_user?.DoctorSpecialization) is { Length: > 0 } s
            ? s
            : (string.IsNullOrWhiteSpace(_user?.DoctorSpecialization) ? "—" : _user!.DoctorSpecialization!);

    // ===== Editable preferences (local) =====
    public ProfilePreferences Preferences => _prefs;

    public string Phone
    {
        get => _prefs.Phone;
        set => UpdatePref(p => p.Phone = value ?? "");
    }

    public string DateOfBirth
    {
        get => _prefs.DateOfBirth;
        set => UpdatePref(p => p.DateOfBirth = value ?? "");
    }

    public string Gender
    {
        get => _prefs.Gender;
        set => UpdatePref(p => p.Gender = value ?? "");
    }

    public string EmergencyContact
    {
        get => _prefs.EmergencyContact;
        set => UpdatePref(p => p.EmergencyContact = value ?? "");
    }

    public string Bio
    {
        get => _prefs.Bio;
        set => UpdatePref(p => p.Bio = value ?? "");
    }

    public string DoctorClinic
    {
        get => _prefs.DoctorClinic;
        set => UpdatePref(p => p.DoctorClinic = value ?? "");
    }

    /// <summary>
    /// Email change requested locally. Empty means no change pending.
    /// The server-side email (<see cref="Email"/>) is untouched until a future backend endpoint syncs this.
    /// </summary>
    public string PendingEmail
    {
        get => _prefs.PendingEmail;
        set => UpdatePref(p => p.PendingEmail = value ?? "");
    }

    public bool HasPendingEmailChange =>
        !string.IsNullOrWhiteSpace(_prefs.PendingEmail) &&
        !string.Equals(_prefs.PendingEmail, Email, StringComparison.OrdinalIgnoreCase);

    public bool IsAvailable
    {
        get => _prefs.IsAvailable;
        set => UpdatePref(p => p.IsAvailable = value);
    }

    public string LanguageCode
    {
        get => _prefs.LanguageCode;
        set
        {
            var v = string.IsNullOrWhiteSpace(value) ? "en" : value;
            Lang.Set(v);
            UpdatePref(p => p.LanguageCode = v);
        }
    }

    // Notifications
    public bool NotifyAiAnalysis
    {
        get => _prefs.NotifyAiAnalysis;
        set => UpdatePref(p => p.NotifyAiAnalysis = value);
    }
    public bool NotifyAppointments
    {
        get => _prefs.NotifyAppointments;
        set => UpdatePref(p => p.NotifyAppointments = value);
    }
    public bool NotifySecurity
    {
        get => _prefs.NotifySecurity;
        set => UpdatePref(p => p.NotifySecurity = value);
    }
    public bool NotifySystem
    {
        get => _prefs.NotifySystem;
        set => UpdatePref(p => p.NotifySystem = value);
    }

    // Privacy
    public bool ProfileVisible
    {
        get => _prefs.ProfileVisible;
        set => UpdatePref(p => p.ProfileVisible = value);
    }
    public bool ShareAnalytics
    {
        get => _prefs.ShareAnalytics;
        set => UpdatePref(p => p.ShareAnalytics = value);
    }
    public bool SaveAiHistory
    {
        get => _prefs.SaveAiHistory;
        set => UpdatePref(p => p.SaveAiHistory = value);
    }
    public bool ActivityTracking
    {
        get => _prefs.ActivityTracking;
        set => UpdatePref(p => p.ActivityTracking = value);
    }

    // ===== Lifecycle =====

    /// <summary>Re-reads current session + preferences. Call on appearing.</summary>
    public void Reload()
    {
        _user = App.CurrentUser;
        _prefs = _prefsService.Load(UserId);

        // Keep global Lang aligned with stored prefs on session load
        if (!string.IsNullOrWhiteSpace(_prefs.LanguageCode) && _prefs.LanguageCode != Lang.Current)
            Lang.Set(_prefs.LanguageCode);

        RaiseAllChanged();
    }

    /// <summary>Replace all editable fields at once (used by Edit Profile dialog).</summary>
    public void ApplyEdit(ProfilePreferences edited)
    {
        if (edited == null)
            return;

        _prefs.Phone = edited.Phone ?? "";
        _prefs.DateOfBirth = edited.DateOfBirth ?? "";
        _prefs.Gender = edited.Gender ?? "";
        _prefs.EmergencyContact = edited.EmergencyContact ?? "";
        _prefs.Bio = edited.Bio ?? "";
        _prefs.DoctorClinic = edited.DoctorClinic ?? "";
        _prefs.IsAvailable = edited.IsAvailable;
        _prefs.PendingEmail = edited.PendingEmail ?? "";

        _prefsService.Save(UserId, _prefs);
        RaiseAllChanged();
    }

    private void UpdatePref(Action<ProfilePreferences> mutate, [CallerMemberName] string? propertyName = null)
    {
        mutate(_prefs);
        _prefsService.Save(UserId, _prefs);
        OnPropertyChanged(propertyName);
    }

    private void RaiseAllChanged()
    {
        // Trigger UI to re-read all bindable values
        OnPropertyChanged(string.Empty);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
}
