using System.Collections;
using BepInEx.Configuration;
using HarmonyLib;
using SilksongTweaks.Rules;
using UnityEngine;

namespace SilksongTweaks.Modules
{
    /// <summary>
    /// Stops rosaries being permanently destroyed when you die twice.
    ///
    /// Vanilla tracks exactly ONE corpse — PlayerData.HeroCorpseMoneyPool plus HeroCorpseScene
    /// and HeroCorpseMarkerGuid — and Die overwrites all three. So a second death before you
    /// recover the first cocoon annihilates it. We snapshot the pool as the death begins and fold
    /// the surviving fraction into the new one.
    ///
    /// WHY THIS WATCHES THE POOL INSTEAD OF WAITING FOR RESPAWN
    /// --------------------------------------------------------
    /// The write happens inside the compiler-generated iterator HeroController/&lt;Die&gt;d__NNNN,
    /// whose name changes on every rebuild of the game, so it cannot be hooked directly. The
    /// first version merged on HeroRespawned instead — but that couples the rescue to an event
    /// that may not fire in every death flow, and when it does not fire the rosaries are lost
    /// silently, which is the worst possible failure for this feature.
    ///
    /// Instead we prefix the enclosing Die (stable name, runs when the enumerator is created)
    /// and then WATCH the pool. The moment the game writes the new value we merge. That depends
    /// on nothing but the field itself.
    ///
    /// Verified by disassembly: HeroCorpseMoneyPool is written in exactly two places, Die and
    /// CocoonBroken, and CocoonBroken reads the pool at break time before paying it out — so a
    /// merged value is honoured whenever you actually collect the cocoon.
    /// </summary>
    public sealed class KeepRosariesModule : ModuleBase
    {
        private const float WatchTimeout = 20f;

        private static KeepRosariesModule _instance;

        private ConfigEntry<float> _keepFraction;
        private ConfigEntry<int> _keepOnDeathPercent;
        private bool _watching;

        public override string Id => "KeepRosaries";
        public override string DisplayName => "Keep rosaries";
        public override string Description => "Dying twice no longer destroys your first cocoon.";

        protected override void BindSettings(ConfigFile config)
        {
            _keepFraction = config.Bind(Id, "KeepFraction", 1f,
                new ConfigDescription(
                    "Fraction of the previous cocoon carried into the new one. 1 = lose nothing, 0 = vanilla.",
                    new AcceptableValueRange<float>(0f, 1f)));

            AddRow(new FloatRow("Keep from old cocoon", "1 = lose nothing, 0 = vanilla behaviour.",
                _keepFraction, 0f, 1f, "0%"));

            _keepOnDeathPercent = config.Bind(Id, "KeepOnDeathPercent", 0,
                new ConfigDescription(
                    "Percent of the rosaries you are CARRYING that stay in hand when you die. " +
                    "0 = vanilla (you drop everything). 100 = death costs you nothing.",
                    new AcceptableValueRange<int>(0, 100)));

            AddRow(new IntRow("Keep on death", "Percent kept in hand instead of dropped.",
                _keepOnDeathPercent, 0, 100));
        }

        protected override TweakStatus Apply(Harmony harmony)
        {
            _instance = this;

            var die = Resolve(HookTargets.Die, out var error);
            if (die == null) return TweakStatus.Unavailable(error);

            harmony.Patch(die, prefix: new HarmonyMethod(
                typeof(KeepRosariesModule), nameof(DiePrefix)));

            return TweakStatus.Active;
        }

        private static void DiePrefix()
        {
            var self = _instance;
            if (self == null || !self.IsOn || self._watching) return;

            var pd = PlayerData.instance;
            if (pd == null) return;

            var poolBefore = pd.HeroCorpseMoneyPool;
            var carrying = pd.geo;

            Plugin.Log.LogInfo(
                $"[KeepRosaries] death: existing cocoon={poolBefore}, carrying={carrying}");

            Plugin.Instance.Run(self.WatchForNewPool(poolBefore, carrying));
        }

        /// <summary>
        /// Waits for Die's coroutine to write the new pool, then folds the old one back in.
        /// Polling is used deliberately: the write is inside a compiler-generated state machine
        /// that cannot be hooked by a stable name.
        /// </summary>
        private IEnumerator WatchForNewPool(int poolBefore, int carriedBefore)
        {
            _watching = true;
            var waited = 0f;

            while (waited < WatchTimeout)
            {
                var pd = PlayerData.instance;

                if (pd != null && pd.HeroCorpseMoneyPool != poolBefore)
                {
                    Merge(pd, poolBefore);
                    ReturnShareToHand(pd, carriedBefore);
                    _watching = false;
                    yield break;
                }

                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            _watching = false;
            Plugin.Log.LogWarning(
                $"[KeepRosaries] pool never changed within {WatchTimeout}s (was {poolBefore}). " +
                "Nothing rescued — if you lost rosaries here, this log line is the reason.");
        }

        /// <summary>
        /// Hands back a share of what you were carrying, taking it out of the cocoon so the same
        /// rosaries cannot be collected twice.
        ///
        /// Uses AddGeoQuietly rather than writing PlayerData.geo directly: the counter on screen is
        /// event-driven, so a raw field write would leave the HUD showing the old total until
        /// something else happened to refresh it. The quiet variant skips the pickup fanfare, which
        /// would be absurd on a death screen.
        /// </summary>
        private void ReturnShareToHand(PlayerData pd, int carriedBefore)
        {
            var percent = _keepOnDeathPercent.Value;
            if (percent <= 0 || carriedBefore <= 0) return;

            var keep = DeathPurseRules.KeepOnDeath(carriedBefore, percent);
            if (keep <= 0) return;

            // Never hand back more than the cocoon actually holds.
            if (keep > pd.HeroCorpseMoneyPool) keep = pd.HeroCorpseMoneyPool;
            if (keep <= 0) return;

            pd.HeroCorpseMoneyPool -= keep;
            CurrencyManager.AddGeoQuietly(keep);
            MarkFired();

            Plugin.Log.LogInfo(
                $"[KeepRosaries] kept {keep} of {carriedBefore} in hand ({percent}%); " +
                $"cocoon now {pd.HeroCorpseMoneyPool}");
        }

        private void Merge(PlayerData pd, int poolBefore)
        {
            var written = pd.HeroCorpseMoneyPool;
            var merged = CocoonRules.Merge(poolBefore, written, _keepFraction.Value);

            if (merged == written)
            {
                Plugin.Log.LogInfo(
                    $"[KeepRosaries] new cocoon={written}, nothing to rescue (previous was {poolBefore})");
                return;
            }

            pd.HeroCorpseMoneyPool = merged;
            MarkFired();

            Plugin.Log.LogInfo(
                $"[KeepRosaries] cocoon {written} -> {merged} " +
                $"(rescued {merged - written} from the previous cocoon of {poolBefore})");
        }
    }
}
