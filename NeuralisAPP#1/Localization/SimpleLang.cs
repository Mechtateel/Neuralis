using Microsoft.Maui.Storage;

namespace NeuralisAPP_1.Localization;

public static class Lang
{
    public static string Current = Preferences.Get("lang", "en");

    public static void Set(string value)
    {
        Current = value;
        Preferences.Set("lang", value);
    }
}

public static class T
{
    public static string Get(string en, string ru, string? kz = null)
    {
        return Lang.Current switch
        {
            "ru" => ru,
            "kz" => kz ?? ru,
            _ => en
        };
    }
}
