using BepInEx.Configuration;
using HarmonyLib;
using SilksongTweaks.Rules;

namespace SilksongTweaks.Modules
{
    /// <summary>
    /// Stops rosaries being permanently destroyed when you die twice.
    ///
    /// Vanilla overwrites PlayerData.HeroCorpseMoneyPool on each death, so a second death before
    /// recovering the first cocoon annihilates it. We snapshot the pool as the death begins and
    /// fold the surviving fraction back in once the game has written the new one.
    ///
    /// The write happens inside the compiler-generated iterator HeroController/&lt;Die&gt;d__NNNN,
    /// whose name changes every time the game is rebuilt. We therefore hook the ENCLOSING Die
    /// method (a prefix there runs when the enumerator is created, before any body code) and
    /// re-apply on HeroRespawned. Same effect, stable across game updates.
    /// </summary>
    public sealed class KeepRosariesModule : ModuleBase
    {
        private static KeepRosariesModule _instance;

        private ConfigEntry<float> _keepFraction;

        private int _poolBeforeDeath;
        private bool _armed;

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
        }

        protected override TweakStatus Apply(Harmony harmony)
        {
            _instance = this;

            var die = Resolve(HookTargets.Die, out var err1);
            if (die == null) return TweakStatus.Unavailable(err1);

            var respawned = Resolve(HookTargets.HeroRespawned, out var err2);
            if (respawned == null) return TweakStatus.Unavailable(err2);

            harmony.Patch(die, prefix: new HarmonyMethod(
                typeof(KeepRosariesModule), nameof(DiePrefix)));

            harmony.Patch(respawned, postfix: new HarmonyMethod(
                typeof(KeepRosariesModule), nameof(HeroRespawnedPostfix)));

            return TweakStatus.Active;
        }

        private static void DiePrefix()
        {
            var self = _instance;
            if (self == null || !self.IsOn) return;

            var pd = PlayerData.instance;
            if (pd == null) return;

            self._poolBeforeDeath = pd.HeroCorpseMoneyPool;
            self._armed = true;
        }

        private static void HeroRespawnedPostfix()
        {
            var self = _instance;
            if (self == null || !self.IsOn || !self._armed) return;

            self._armed = false;

            var pd = PlayerData.instance;
            if (pd == null) return;

            var merged = CocoonRules.Merge(
                self._poolBeforeDeath, pd.HeroCorpseMoneyPool, self._keepFraction.Value);

            if (merged == pd.HeroCorpseMoneyPool) return;

            Plugin.Log.LogInfo(
                $"[KeepRosaries] cocoon {pd.HeroCorpseMoneyPool} -> {merged} " +
                $"(rescued {merged - pd.HeroCorpseMoneyPool} from the previous cocoon)");

            pd.HeroCorpseMoneyPool = merged;
            self.MarkFired();
        }
    }
}
