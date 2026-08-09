using System.Collections.Generic;

namespace SilksongTweaks
{
    /// <summary>What kind of member a hook target is, so the verifier knows how to look it up.</summary>
    public enum HookKind
    {
        Method,
        PropertyGetter,
        Field,
    }

    /// <summary>A single member of the game we depend on.</summary>
    public sealed class HookTarget
    {
        public HookTarget(string declaringType, string member, HookKind kind, string usedBy)
        {
            DeclaringType = declaringType;
            Member = member;
            Kind = kind;
            UsedBy = usedBy;
        }

        public string DeclaringType { get; }
        public string Member { get; }
        public HookKind Kind { get; }

        /// <summary>Which module needs it — makes a failing verification test self-explanatory.</summary>
        public string UsedBy { get; }

        public override string ToString() => $"{DeclaringType}.{Member} ({Kind}, for {UsedBy})";
    }

    /// <summary>
    /// THE single source of truth for every game member this mod depends on.
    ///
    /// Both the runtime modules and the hook-verification test read this list. It deliberately
    /// has no Unity or BepInEx dependency so the test project can compile it directly — a test
    /// that kept its own copy would verify a duplicate and pass while the real thing broke.
    ///
    /// Every entry below was confirmed present in Assembly-CSharp.dll of Silksong build
    /// 1.0.30000 on 2026-08-09 by reading the assembly, not by guessing from strings.
    ///
    /// NOTE ON COMPILER-GENERATED MEMBERS: nothing here targets an iterator state machine such
    /// as HeroController/&lt;Die&gt;d__1101. Those numeric suffixes are emitted by the compiler and
    /// change on every rebuild of the game, so patching them breaks silently on any update.
    /// Where the logic we care about lives inside a coroutine, we hook the enclosing method.
    /// </summary>
    public static class HookTargets
    {
        // --- Return to death location -------------------------------------------------------
        public static readonly HookTarget HeroRespawned =
            new HookTarget("HeroController", "HeroRespawned", HookKind.Method, "ReturnToDeath");

        public static readonly HookTarget Die =
            new HookTarget("HeroController", "Die", HookKind.Method, "ReturnToDeath, KeepRosaries");

        public static readonly HookTarget BeginSceneTransition =
            new HookTarget("GameManager", "BeginSceneTransition", HookKind.Method, "ReturnToDeath");

        public static readonly HookTarget SetHazardRespawn =
            new HookTarget("HeroController", "SetHazardRespawn", HookKind.Method, "ReturnToDeath");

        // --- Keep rosaries ------------------------------------------------------------------
        public static readonly HookTarget CocoonBroken =
            new HookTarget("HeroController", "CocoonBroken", HookKind.Method, "KeepRosaries");

        public static readonly HookTarget CorpseMoneyPool =
            new HookTarget("PlayerData", "HeroCorpseMoneyPool", HookKind.Field, "KeepRosaries");

        // --- Max health ---------------------------------------------------------------------
        public static readonly HookTarget CurrentMaxHealth =
            new HookTarget("PlayerData", "CurrentMaxHealth", HookKind.PropertyGetter, "MaxHealth");

        public static readonly HookTarget GetInt =
            new HookTarget("PlayerData", "GetInt", HookKind.Method, "MaxHealth");

        public static readonly HookTarget MaxHealthField =
            new HookTarget("PlayerData", "maxHealth", HookKind.Field, "MaxHealth");

        // Transpiled to restore the CurrentMaxHealth == maxHealth invariant. AddToMaxHealth is
        // deliberately absent: it is the mask-shard upgrade path and writes both members.
        public static readonly HookTarget PlayerDataAddHealth =
            new HookTarget("PlayerData", "AddHealth", HookKind.Method, "MaxHealth");

        public static readonly HookTarget PlayerDataTakeHealth =
            new HookTarget("PlayerData", "TakeHealth", HookKind.Method, "MaxHealth");

        public static readonly HookTarget PlayerDataMaxHealth =
            new HookTarget("PlayerData", "MaxHealth", HookKind.Method, "MaxHealth");

        // --- Damage taken -------------------------------------------------------------------
        public static readonly HookTarget TakeDamage =
            new HookTarget("HeroController", "TakeDamage", HookKind.Method, "DamageTaken");

        /// <summary>Every target, for the verification test.</summary>
        public static IReadOnlyList<HookTarget> All { get; } = new List<HookTarget>
        {
            HeroRespawned,
            Die,
            BeginSceneTransition,
            SetHazardRespawn,
            CocoonBroken,
            CorpseMoneyPool,
            CurrentMaxHealth,
            GetInt,
            MaxHealthField,
            PlayerDataAddHealth,
            PlayerDataTakeHealth,
            PlayerDataMaxHealth,
            TakeDamage,
        };
    }
}
