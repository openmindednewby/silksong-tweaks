using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace SilksongTweaks.Modules
{
    /// <summary>
    /// Shared plumbing for tweak modules: status tracking, "last fired" reporting, and the
    /// guarded patch helper that keeps one broken module from taking down the others.
    /// </summary>
    public abstract class ModuleBase : ITweakModule
    {
        private readonly List<ISettingRow> _rows = new List<ISettingRow>();

        protected ConfigEntry<bool> EnabledEntry { get; private set; }

        protected ManualLogSource Log => Plugin.Log;

        public abstract string Id { get; }
        public abstract string DisplayName { get; }
        public abstract string Description { get; }

        public TweakStatus Status { get; protected set; } = TweakStatus.Unavailable("not applied yet");

        public long LastFiredUtcTicks { get; private set; }

        public IReadOnlyList<ISettingRow> Settings => _rows;

        /// <summary>True only when hooks attached AND the user has it switched on.</summary>
        public bool IsOn => Status.State != TweakState.Unavailable
                            && EnabledEntry != null
                            && EnabledEntry.Value;

        public void BindConfig(ConfigFile config)
        {
            EnabledEntry = config.Bind(Id, "Enabled", true, Description);
            AddRow(new BoolRow("Enabled", Description, EnabledEntry));
            BindSettings(config);
        }

        protected abstract void BindSettings(ConfigFile config);

        protected void AddRow(ISettingRow row) => _rows.Add(row);

        public TweakStatus TryApply(Harmony harmony)
        {
            try
            {
                Status = Apply(harmony);
            }
            catch (Exception ex)
            {
                Status = TweakStatus.Unavailable(ex.Message);
                Log.LogError($"[{Id}] failed to apply: {ex}");
            }

            return Status;
        }

        protected abstract TweakStatus Apply(Harmony harmony);

        /// <summary>Record that this tweak actually did something, for the UI readout.</summary>
        public void MarkFired() => LastFiredUtcTicks = DateTime.UtcNow.Ticks;

        /// <summary>
        /// Resolve a hook target or explain precisely which one is missing. Returning null here
        /// is what produces a greyed-out row with a reason instead of a silent no-op.
        /// </summary>
        protected static System.Reflection.MethodBase Resolve(HookTarget target, out string error)
        {
            var type = AccessTools.TypeByName(target.DeclaringType);
            if (type == null)
            {
                error = $"type '{target.DeclaringType}' not found";
                return null;
            }

            System.Reflection.MethodBase method =
                target.Kind == HookKind.PropertyGetter
                    ? AccessTools.PropertyGetter(type, target.Member)
                    : AccessTools.Method(type, target.Member);

            if (method == null)
            {
                error = $"{target.DeclaringType}.{target.Member} not found";
                return null;
            }

            error = null;
            return method;
        }
    }
}
