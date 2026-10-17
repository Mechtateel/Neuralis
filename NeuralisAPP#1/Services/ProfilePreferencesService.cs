using System.Text.Json;
using Microsoft.Maui.Storage;
using NeuralisAPP_1.Localization;
using NeuralisAPP_1.Models.Profile;

namespace NeuralisAPP_1.Services;

/// <summary>
/// Lightweight local-first persistence for profile preferences.
/// Uses <see cref="Preferences"/> keyed by user id, so multiple accounts on the same device
/// keep independent state. Designed to swap for an HTTP-backed implementation later
/// without touching consumers.
/// </summary>
public sealed class ProfilePreferencesService
{
    private const string PrefsKeyPrefix = "profile.prefs.";

    public static ProfilePreferencesService Instance { get; } = new();

    /// <summary>Raised whenever preferences are saved. UI may refresh derived state.</summary>
    public event EventHandler? PreferencesChanged;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    public ProfilePreferences Load(Guid userId)
    {
        var key = ResolveKey(userId);
        var raw = Preferences.Get(key, string.Empty);

        ProfilePreferences prefs;
        if (string.IsNullOrWhiteSpace(raw))
        {
            prefs = new ProfilePreferences { LanguageCode = Lang.Current };
        }
        else
        {
            try
            {
                prefs = JsonSerializer.Deserialize<ProfilePreferences>(raw, JsonOptions)
                        ?? new ProfilePreferences();
            }
            catch (JsonException)
            {
                prefs = new ProfilePreferences { LanguageCode = Lang.Current };
            }
        }

        // Keep language in sync with the global Lang on first load
        if (string.IsNullOrWhiteSpace(prefs.LanguageCode))
            prefs.LanguageCode = Lang.Current;

        return prefs;
    }

    public void Save(Guid userId, ProfilePreferences prefs)
    {
        if (prefs == null)
            return;

        var key = ResolveKey(userId);
        var json = JsonSerializer.Serialize(prefs, JsonOptions);
        Preferences.Set(key, json);

        PreferencesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Helper for one-off field updates that re-persists in a single call.</summary>
    public void Update(Guid userId, Action<ProfilePreferences> mutate)
    {
        var prefs = Load(userId);
        mutate(prefs);
        Save(userId, prefs);
    }

    private static string ResolveKey(Guid userId) =>
        userId == Guid.Empty
            ? PrefsKeyPrefix + "anon"
            : PrefsKeyPrefix + userId.ToString("N");
}
