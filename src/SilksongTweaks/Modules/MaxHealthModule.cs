using BepInEx.Configuration;
using HarmonyLib;
using SilksongTweaks.Rules;

namespace SilksongTweaks.Modules
{
    /// <summary>
    /// Raises Hornet's maximum masks.
    ///
    /// READ-SIDE ONLY, deliberately. Both hooks intercept reads of max health and neither ever
    /// writes to PlayerData, so nothing reaches the save file. Uninstalling the mod returns the
    /// save to vanilla with nothing to undo.
    ///
    /// Two hooks are needed because the game reads max health two different ways:
    ///   - PlayerData.CurrentMaxHealth  — game logic (healing, bench refill, taking damage)
    ///   - PlayerData.GetInt("maxHealth") — the PlayMaker FSMs that draw the mask HUD
    /// Patching only the first would give you the extra health with a HUD that still showed the
    /// old mask count.
    /// </summary>
    public sealed class MaxHealthModule : ModuleBase
    {
        private const string MaxHealthKey = "maxHealth";

        private static MaxHealthModule _instance;

        private ConfigEntry<int> _masks;

        public override string Id => "MaxHealth";
        public override string DisplayName => "Max masks";
        public override string Description => "Raise Hornet's maximum health.";

        protected override void BindSettings(ConfigFile config)
        {
            _masks = config.Bind(Id, "Masks", 8,
                new ConfigDescription(
                    "Maximum masks. Never lowers you below what you have earned in-game.",
                    new AcceptableValueRange<int>(HealthRules.MinMasks, HealthRules.MaxMasks)));

            AddRow(new IntRow("Masks", "How many masks Hornet has at full health.",
                _masks, HealthRules.MinMasks, HealthRules.MaxMasks));
        }

        protected override TweakStatus Apply(Harmony harmony)
        {
            _instance = this;

            var getter = Resolve(HookTargets.CurrentMaxHealth, out var err1);
            if (getter == null) return TweakStatus.Unavailable(err1);

            var getInt = Resolve(HookTargets.GetInt, out var err2);
            if (getInt == null) return TweakStatus.Unavailable(err2);

            harmony.Patch(getter, postfix: new HarmonyMethod(
                typeof(MaxHealthModule), nameof(CurrentMaxHealthPostfix)));

            harmony.Patch(getInt, postfix: new HarmonyMethod(
                typeof(MaxHealthModule), nameof(GetIntPostfix)));

            return TweakStatus.Active;
        }

        private static void CurrentMaxHealthPostfix(ref int __result)
        {
            var self = _instance;
            if (self == null || !self.IsOn) return;

            var resolved = HealthRules.Resolve(__result, self._masks.Value, enabled: true);
            if (resolved == __result) return;

            __result = resolved;
            self.MarkFired();
        }

        private static void GetIntPostfix(string intName, ref int __result)
        {
            var self = _instance;
            if (self == null || !self.IsOn) return;
            if (intName != MaxHealthKey) return;

            __result = HealthRules.Resolve(__result, self._masks.Value, enabled: true);
        }
    }
}
