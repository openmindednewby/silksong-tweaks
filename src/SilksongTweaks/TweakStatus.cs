namespace SilksongTweaks
{
    /// <summary>
    /// Whether a module's hooks actually attached. The UI renders each state differently so a
    /// tweak that failed to hook is visibly distinct from one that is merely switched off —
    /// the distinction that is invisible in every mod this project replaces.
    /// </summary>
    public enum TweakState
    {
        /// <summary>Hooks attached and the user has it switched on.</summary>
        Active,

        /// <summary>Hooks attached; the user switched it off. The patch short-circuits.</summary>
        Disabled,

        /// <summary>A hook target was missing, or patching threw. The tweak cannot work.</summary>
        Unavailable,
    }

    /// <summary>Immutable status with the reason a tweak is unavailable, when it is.</summary>
    public sealed class TweakStatus
    {
        private TweakStatus(TweakState state, string reason)
        {
            State = state;
            Reason = reason;
        }

        public TweakState State { get; }

        /// <summary>Human-readable explanation. Empty unless <see cref="TweakState.Unavailable"/>.</summary>
        public string Reason { get; }

        public static TweakStatus Active { get; } = new TweakStatus(TweakState.Active, string.Empty);

        public static TweakStatus Disabled { get; } = new TweakStatus(TweakState.Disabled, string.Empty);

        public static TweakStatus Unavailable(string reason) =>
            new TweakStatus(TweakState.Unavailable, reason ?? "unknown");
    }
}
