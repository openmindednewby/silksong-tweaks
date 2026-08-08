using BepInEx.Configuration;
using HarmonyLib;
using SilksongTweaks.Rules;

namespace SilksongTweaks.Modules
{
    /// <summary>
    /// Scales incoming damage to Hornet.
    ///
    /// Hooks HeroController.TakeDamage and rewrites the damage amount before the game applies it,
    /// so knockback, i-frames and hit reactions all behave normally — only the number changes.
    /// </summary>
    public sealed class DamageTakenModule : ModuleBase
    {
        private const float MinMultiplier = 0f;
        private const float MaxMultiplier = 3f;

        private static DamageTakenModule _instance;

        private ConfigEntry<float> _multiplier;

        public override string Id => "DamageTaken";
        public override string DisplayName => "Damage taken";
        public override string Description => "Scale how much damage Hornet takes.";

        protected override void BindSettings(ConfigFile config)
        {
            _multiplier = config.Bind(Id, "Multiplier", 1f,
                new ConfigDescription(
                    "1 = vanilla. 0.5 = half damage. 0 = invulnerable. Above 1 makes the game harder.",
                    new AcceptableValueRange<float>(MinMultiplier, MaxMultiplier)));

            AddRow(new FloatRow("Multiplier", "1 = vanilla, 0 = invulnerable.",
                _multiplier, MinMultiplier, MaxMultiplier, "0.00x"));
        }

        protected override TweakStatus Apply(Harmony harmony)
        {
            _instance = this;

            var takeDamage = Resolve(HookTargets.TakeDamage, out var err);
            if (takeDamage == null) return TweakStatus.Unavailable(err);

            harmony.Patch(takeDamage, prefix: new HarmonyMethod(
                typeof(DamageTakenModule), nameof(TakeDamagePrefix)));

            return TweakStatus.Active;
        }

        private static void TakeDamagePrefix(ref int damageAmount)
        {
            var self = _instance;
            if (self == null || !self.IsOn) return;
            if (damageAmount <= 0) return;

            var scaled = DamageRules.Scale(damageAmount, self._multiplier.Value);
            if (scaled == damageAmount) return;

            damageAmount = scaled;
            self.MarkFired();
        }
    }
}
