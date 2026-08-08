namespace SilksongTweaks.Rules
{
    /// <summary>
    /// Every decision this mod makes, as pure functions over primitives.
    ///
    /// Deliberately free of Unity and BepInEx types so they run in a normal xUnit project in
    /// milliseconds. The Harmony patches are thin shims: read game state, call a rule here,
    /// write the result back. The bugs live in this file, so this is what gets tested.
    /// </summary>
    public static class DamageRules
    {
        /// <summary>
        /// Scale incoming damage. Never turns a real hit into zero unless the player explicitly
        /// asked for invulnerability (multiplier 0), because a hit that deals no damage but still
        /// triggers knockback and i-frames reads as a broken game rather than a merciful one.
        /// </summary>
        public static int Scale(int incoming, float multiplier)
        {
            if (incoming <= 0) return incoming;
            if (multiplier <= 0f) return 0;

            var scaled = (int)System.Math.Round(incoming * (double)multiplier);
            if (scaled < 1) scaled = 1;
            return scaled;
        }
    }

    public static class HealthRules
    {
        public const int MinMasks = 1;
        public const int MaxMasks = 20;

        /// <summary>
        /// Resolve the max health the game should see.
        ///
        /// The configured value replaces the vanilla base rather than adding to it, so the slider
        /// reads as an absolute "how many masks do I have". Mask shards already collected are
        /// still honoured on top, so upgrading never makes you weaker than the slider promises.
        /// </summary>
        public static int Resolve(int vanillaMax, int configuredMasks, bool enabled)
        {
            if (!enabled) return vanillaMax;

            var target = Clamp(configuredMasks, MinMasks, MaxMasks);
            return vanillaMax > target ? vanillaMax : target;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }

    public static class CocoonRules
    {
        /// <summary>
        /// How many rosaries the new death cocoon should hold.
        ///
        /// Vanilla replaces the pool, so dying twice destroys the first cocoon's contents. Here
        /// the surviving fraction of the previous pool is carried into the new one. A fraction of
        /// 1 loses nothing; 0 reproduces vanilla exactly, which is what makes the setting a
        /// genuine dial rather than an on/off cheat.
        /// </summary>
        public static int Merge(int previousPool, int carriedThisDeath, float keepFraction)
        {
            if (previousPool < 0) previousPool = 0;
            if (carriedThisDeath < 0) carriedThisDeath = 0;

            if (keepFraction <= 0f) return carriedThisDeath;
            if (keepFraction > 1f) keepFraction = 1f;

            var kept = (int)(previousPool * (double)keepFraction);
            var total = (long)kept + carriedThisDeath;

            return total > int.MaxValue ? int.MaxValue : (int)total;
        }
    }
}
