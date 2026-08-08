using System.Globalization;
using System.Resources;

namespace RenewTheDoc.App.Localization;

/// <summary>Localized string lookup. Follows the system locale; falls back to English (neutral resx).</summary>
public static class L
{
    private static readonly ResourceManager Resources =
        new("RenewTheDoc.App.Resources.Strings.AppStrings", typeof(L).Assembly);

    public static string T(string key) =>
        Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string F(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(key), args);
}
