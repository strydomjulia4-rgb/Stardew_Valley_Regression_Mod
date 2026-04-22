using StardewModdingAPI;
using System;

namespace PrimevalTitmouse
{
    /// <summary>
    /// Minimal GMCM API contract used only to acquire/register safely.
    /// </summary>
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddSectionTitle(IManifest mod, Func<string> text, Func<string> tooltip = null);
        void AddTextOption(
            IManifest mod,
            Func<string> getValue,
            Action<string> setValue,
            Func<string> name,
            Func<string> tooltip = null,
            string[] allowedValues = null,
            Func<string, string> formatAllowedValue = null
        );
    }
}
