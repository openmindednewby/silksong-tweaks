using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Configuration;
using HarmonyLib;
using SilksongTweaks.Rules;

namespace SilksongTweaks.Modules
{
    /// <summary>
    /// Raises Hornet's maximum masks.
    ///
    /// READ-SIDE ONLY: nothing here ever writes PlayerData, so nothing reaches the save file and
    /// uninstalling returns the save to vanilla with nothing to undo.
    ///
    /// WHY THIS NEEDS A TRANSPILER
    /// ---------------------------
    /// The game holds an invariant that CurrentMaxHealth == maxHealth:
    ///     CurrentMaxHealth => BoundShell ? Min(maxHealth, BoundMaxHealth) : maxHealth;
    /// and it uses the property and the field INTERCHANGEABLY inside the same method. Raising
    /// only the property breaks the invariant, and the game then takes branches written for a
    /// completely different situation:
    ///
    ///   AddHealth   clamps healing to the FIELD          -> healing capped at vanilla max
    ///   TakeHealth  tops health up to the PROPERTY first -> taking a hit at full health RAISES it
    ///
    /// So the fix is not "patch more reads", it is "restore the invariant": inside the health
    /// methods, rewrite every `ldfld maxHealth` into `call get_CurrentMaxHealth`. Both are one
    /// stack slot in, one int out, so the substitution is stack-neutral, and every branch then
    /// behaves exactly as Team Cherry wrote it.
    ///
    /// The getter itself is deliberately NOT transpiled: it reads maxHealth, so rewriting it
    /// would make it call itself forever.
    /// </summary>
    public sealed class MaxHealthModule : ModuleBase
    {
        private const string MaxHealthKey = "maxHealth";
        private const string MaxHealthField = "maxHealth";

        private static MaxHealthModule _instance;
        private static int _rewritten;

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

            var getter = Resolve(HookTargets.CurrentMaxHealth, out var err);
            if (getter == null) return TweakStatus.Unavailable(err);

            var getInt = Resolve(HookTargets.GetInt, out err);
            if (getInt == null) return TweakStatus.Unavailable(err);

            harmony.Patch(getter, postfix: new HarmonyMethod(
                typeof(MaxHealthModule), nameof(CurrentMaxHealthPostfix)));

            harmony.Patch(getInt, postfix: new HarmonyMethod(
                typeof(MaxHealthModule), nameof(GetIntPostfix)));

            var status = RestoreInvariantIn(harmony);
            if (status != null) return status;

            if (_rewritten == 0)
            {
                return TweakStatus.Unavailable(
                    "no maxHealth field reads found to rewrite — the game's health code changed");
            }

            Log.LogInfo($"[MaxHealth] restored max-health invariant at {_rewritten} site(s)");
            return TweakStatus.Active;
        }

        /// <summary>
        /// Transpile the three methods that mix the field and the property. Deliberately excludes
        /// AddToMaxHealth, which is the mask-shard upgrade path and WRITES both members: rewriting
        /// it would corrupt real progression.
        /// </summary>
        private TweakStatus RestoreInvariantIn(Harmony harmony)
        {
            var targets = new[]
            {
                HookTargets.PlayerDataAddHealth,
                HookTargets.PlayerDataTakeHealth,
                HookTargets.PlayerDataMaxHealth,
            };

            var transpiler = new HarmonyMethod(typeof(MaxHealthModule), nameof(MaxHealthTranspiler));

            foreach (var target in targets)
            {
                var method = Resolve(target, out var error);
                if (method == null) return TweakStatus.Unavailable(error);

                harmony.Patch(method, transpiler: transpiler);
            }

            return null;
        }

        private static IEnumerable<CodeInstruction> MaxHealthTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var getter = AccessTools.PropertyGetter(typeof(PlayerData), "CurrentMaxHealth");

            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldfld
                    && instruction.operand is FieldInfo field
                    && field.Name == MaxHealthField
                    && field.DeclaringType == typeof(PlayerData))
                {
                    // Mutated in place rather than replaced, so any branch labels or exception
                    // blocks attached to this instruction survive. A replacement instruction
                    // would silently drop them and corrupt control flow.
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = getter;
                    _rewritten++;
                }

                yield return instruction;
            }
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
