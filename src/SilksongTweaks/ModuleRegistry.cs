using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace SilksongTweaks
{
    /// <summary>
    /// Owns the module list and applies them independently, so a module whose hook target has
    /// vanished cannot prevent the others from working.
    /// </summary>
    public sealed class ModuleRegistry
    {
        private readonly List<ITweakModule> _modules = new List<ITweakModule>();
        private readonly ManualLogSource _log;

        public ModuleRegistry(ManualLogSource log) => _log = log;

        public IReadOnlyList<ITweakModule> Modules => _modules;

        public int ActiveCount { get; private set; }

        public void Add(ITweakModule module) => _modules.Add(module);

        public void ApplyAll(ConfigFile config, Harmony harmony)
        {
            ActiveCount = 0;

            foreach (var module in _modules)
            {
                try
                {
                    module.BindConfig(config);
                }
                catch (Exception ex)
                {
                    _log.LogError($"[{module.Id}] config binding failed: {ex}");
                    continue;
                }

                var status = module.TryApply(harmony);

                if (status.State == TweakState.Unavailable)
                {
                    _log.LogWarning($"[{module.Id}] UNAVAILABLE: {status.Reason}");
                    continue;
                }

                ActiveCount++;
                _log.LogInfo($"[{module.Id}] hooked OK");
            }

            _log.LogInfo($"{ActiveCount}/{_modules.Count} tweak(s) hooked successfully");
        }
    }
}
