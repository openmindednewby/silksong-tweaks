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

        // --- Rosary multiplier ---------------------------------------------------------------
        // GeoControl is the physical rosary pickup. Its Collected() is the ONLY currency path
        // that means "the player walked over a dropped rosary", which is what we want to
        // multiply. CurrencyManager.AddGeo is shared: CocoonBroken reaches it via AddCurrency ->
        // ProcessAddCurrency, so multiplying AddGeo unconditionally would also inflate the death
        // cocoon and double-dip with KeepRosaries.
        public static readonly HookTarget GeoControlCollected =
            new HookTarget("GeoControl", "Collected", HookKind.Method, "RosaryMultiplier");

        public static readonly HookTarget CurrencyManagerAddGeo =
            new HookTarget("CurrencyManager", "AddGeo", HookKind.Method, "RosaryMultiplier");

        // --- Map pins -------------------------------------------------------------------------
        // The pin flags have NO direct IL readers except get_HasAnyPin, so the map UI reads them
        // through PlayerData.GetBool(name) — the PlayMaker string path. Postfixing GetBool is
        // therefore read-side only and never writes the save. HasAnyPin reads the FIELDS though,
        // so it needs its own postfix or the map's pin legend stays hidden.
        // MapPin.IsActive is the gate. CanBeActive checks it FIRST and only then checks
        // parentScene.IsMapped / IsVisited, so forcing it can never reveal a room you have not
        // been to — the discovery test still runs afterwards.
        public static readonly HookTarget MapPinIsActive =
            new HookTarget("MapPin", "IsActive", HookKind.PropertyGetter, "MapPins");

        public static readonly HookTarget HasAnyPin =
            new HookTarget("PlayerData", "HasAnyPin", HookKind.PropertyGetter, "MapPins");

        public static readonly HookTarget PinBenchField =
            new HookTarget("PlayerData", "hasPinBench", HookKind.Field, "MapPins");

        // --- Controller lock ----------------------------------------------------------------
        // Silksong reads gamepads through InControl, which is compiled into Assembly-CSharp
        // rather than shipped as its own assembly, so these resolve out of the same DLL as the
        // targets above. Update is declared only on the base PlayerActionSet — no subclass
        // overrides it — so one hook there covers every action set the game creates.
        public static readonly HookTarget PlayerActionSetUpdate =
            new HookTarget("InControl.PlayerActionSet", "Update", HookKind.Method, "ControllerLock");

        public static readonly HookTarget PlayerActionSetIncludeDevices =
            new HookTarget("InControl.PlayerActionSet", "IncludeDevices",
                HookKind.PropertyGetter, "ControllerLock");

        public static readonly HookTarget InputManagerDevices =
            new HookTarget("InControl.InputManager", "Devices", HookKind.Field, "ControllerLock");

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
            GeoControlCollected,
            CurrencyManagerAddGeo,
            MapPinIsActive,
            HasAnyPin,
            PinBenchField,
            PlayerActionSetUpdate,
            PlayerActionSetIncludeDevices,
            InputManagerDevices,
        };
    }
}
