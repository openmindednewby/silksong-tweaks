using BepInEx.Configuration;
using HarmonyLib;
using SilksongTweaks.Rules;

namespace SilksongTweaks.Modules
{
    /// <summary>
    /// Multiplies the rosaries you pick up from defeated enemies.
    ///
    /// WHY TWO HOOKS INSTEAD OF ONE
    /// -----------------------------
    /// The obvious hook is CurrencyManager.AddGeo, but it is SHARED. Cross-referencing the
    /// callers shows the death cocoon reaches it too:
    ///
    ///     CocoonBroken -> CurrencyManager.AddCurrency -> ProcessAddCurrency -> AddGeo
    ///
    /// Multiplying AddGeo unconditionally would therefore inflate your own recovered cocoon and
    /// double-dip with the KeepRosaries tweak — you would be paid a multiple of rosaries you had
    /// already earned, which is not what "more drops from enemies" means.
    ///
    /// So we gate on the source instead. GeoControl is the physical rosary pickup; its Collected()
    /// runs when you walk over one, and calls AddGeo synchronously. A prefix/finalizer pair around
    /// Collected marks that window, and AddGeo only multiplies inside it. Cocoon payouts, chest
    /// pickups and quest rewards pass through untouched.
    ///
    /// SCOPE, STATED HONESTLY: this covers everything you physically pick up — enemy drops AND
    /// rosary caches in the world. Separating those would mean hooking the spawn side in
    /// HealthManager for very little gain.
    /// </summary>
    public sealed class RosaryMultiplierModule : ModuleBase
    {
        private static RosaryMultiplierModule _instance;

        /// <summary>
        /// Depth, not a bool. Nested or re-entrant collection would make a bool clear the window
        /// early and silently stop multiplying, which is exactly the kind of failure that looks
        /// like "the setting does nothing".
        /// </summary>
        private static int _collectDepth;

        private ConfigEntry<float> _multiplier;

        public override string Id => "RosaryMultiplier";
        public override string DisplayName => "Rosary multiplier";
        public override string Description => "Multiply rosaries picked up from defeated enemies.";

        protected override void BindSettings(ConfigFile config)
        {
            _multiplier = config.Bind(Id, "Multiplier", 1f,
                new ConfigDescription(
                    "Multiplies rosaries you pick up. 1 = vanilla. Does not affect your death cocoon.",
                    new AcceptableValueRange<float>(MoneyRules.MinMultiplier, MoneyRules.MaxMultiplier)));

            AddRow(new FloatRow("Multiplier", "1 = vanilla. Applies to rosaries you pick up.",
                _multiplier, MoneyRules.MinMultiplier, MoneyRules.MaxMultiplier, "0.00x"));
        }

        protected override TweakStatus Apply(Harmony harmony)
        {
            _instance = this;

            var collected = Resolve(HookTargets.GeoControlCollected, out var error);
            if (collected == null) return TweakStatus.Unavailable(error);

            var addGeo = Resolve(HookTargets.CurrencyManagerAddGeo, out error);
            if (addGeo == null) return TweakStatus.Unavailable(error);

            harmony.Patch(collected,
                prefix: new HarmonyMethod(typeof(RosaryMultiplierModule), nameof(CollectedPrefix)),
                finalizer: new HarmonyMethod(typeof(RosaryMultiplierModule), nameof(CollectedFinalizer)));

            harmony.Patch(addGeo,
                prefix: new HarmonyMethod(typeof(RosaryMultiplierModule), nameof(AddGeoPrefix)));

            return TweakStatus.Active;
        }

        private static void CollectedPrefix() => _collectDepth++;

        /// <summary>
        /// A finalizer rather than a postfix: it runs even if Collected throws. A postfix would be
        /// skipped on an exception, leaving the window stuck open and multiplying every later
        /// currency gain including the death cocoon.
        /// </summary>
        private static void CollectedFinalizer()
        {
            if (_collectDepth > 0) _collectDepth--;
        }

        private static void AddGeoPrefix(ref int amount)
        {
            var self = _instance;
            if (self == null || !self.IsOn) return;
            if (_collectDepth <= 0) return;

            var scaled = MoneyRules.Scale(amount, self._multiplier.Value);
            if (scaled == amount) return;

            amount = scaled;
            self.MarkFired();
        }
    }
}
