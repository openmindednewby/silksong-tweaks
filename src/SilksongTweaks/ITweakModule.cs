using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;

namespace SilksongTweaks
{
    /// <summary>
    /// One user-facing behaviour. The UI knows only what this exposes — name, settings, status —
    /// and never what a module does, so adding a tweak touches no UI code.
    /// </summary>
    public interface ITweakModule
    {
        /// <summary>Stable identifier, also used as the config section name.</summary>
        string Id { get; }

        /// <summary>Shown as the section heading in the panel.</summary>
        string DisplayName { get; }

        /// <summary>One line explaining what it does, shown under the heading.</summary>
        string Description { get; }

        /// <summary>Current hook/enable state. Set during <see cref="TryApply"/>.</summary>
        TweakStatus Status { get; }

        /// <summary>UTC ticks when this tweak last did something, or 0 if never.</summary>
        long LastFiredUtcTicks { get; }

        /// <summary>Rows the UI should render, in order.</summary>
        IReadOnlyList<ISettingRow> Settings { get; }

        /// <summary>Bind config entries. Called once, before <see cref="TryApply"/>.</summary>
        void BindConfig(ConfigFile config);

        /// <summary>
        /// Attach hooks. Must not throw: return a failure status instead so one broken module
        /// cannot take down the others.
        /// </summary>
        TweakStatus TryApply(Harmony harmony);
    }
}
