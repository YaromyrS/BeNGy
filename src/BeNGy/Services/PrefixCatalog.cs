using BeNGy.Models;
using BeNGy.Services.Interfaces;

namespace BeNGy.Services;

/// <summary>
/// The user-managed list of branch prefixes, persisted as a comma-joined
/// preference. Both the main palette picker and the settings page read it;
/// the settings page edits it.
/// </summary>
public static class PrefixCatalog
{
    public static List<string> Load(IPreferencesService? preferences)
    {
        var raw = preferences?.Get(PreferenceKeys.BranchPrefixes, PreferenceKeys.BranchPrefixesDefault)
                  ?? PreferenceKeys.BranchPrefixesDefault;

        var list = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                      .Distinct()
                      .ToList();

        return list.Count > 0
            ? list
            : PreferenceKeys.BranchPrefixesDefault.Split(',').ToList();
    }

    public static void Save(IPreferencesService? preferences, IEnumerable<string> prefixes)
    {
        preferences?.Set(PreferenceKeys.BranchPrefixes, string.Join(',', prefixes));
    }
}
