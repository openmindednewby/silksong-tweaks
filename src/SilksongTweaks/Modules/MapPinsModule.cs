using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;

namespace SilksongTweaks.Modules
{
    /// <summary>
    /// Shows benches, fast-travel points and other landmarks on the map without buying the pins.
    ///
    /// IMPORTANT — THIS DOES NOT REVEAL THE MAP. Pins only mark places on rooms you have already
    /// explored, exactly as the bought pins do in vanilla. Somewhere you have never been stays
    /// blank. That is a different flag entirely (PlayerData.mapAllRooms) and this module
    /// deliberately does not touch it: revealing unexplored map is not what "show me the benches
    /// I have found" means, and it would spoil the game.
    ///
    /// READ-SIDE ONLY, so nothing reaches the save file. Cross-referencing shows the pin flags
    /// have no direct IL readers apart from get_HasAnyPin, which means the map UI reads them
    /// through PlayerData.GetBool(name) — the string-keyed PlayMaker path. Postfixing that
    /// intercepts the read and leaves the stored value false, so uninstalling restores vanilla
    /// with nothing to undo and you can still buy the real pins later.
    ///
    /// HasAnyPin needs its own hook because it reads the FIELDS directly rather than going
    /// through GetBool — the same field-versus-accessor split that broke max health. Without it
    /// the map would honour individual pins but still consider you to own none, hiding the legend.
    /// </summary>
    public sealed class MapPinsModule : ModuleBase
    {
        private const string BenchFlag = "hasPinBench";
        private const string StagFlag = "hasPinStag";
        private const string TubeFlag = "hasPinTube";
        private const string ShopFlag = "hasPinShop";
        private const string SpaFlag = "hasPinSpa";
        private const string CocoonFlag = "hasPinCocoon";

        private static MapPinsModule _instance;

        private ConfigEntry<bool> _benches;
        private ConfigEntry<bool> _fastTravel;
        private ConfigEntry<bool> _shops;
        private ConfigEntry<bool> _cocoon;

        public override string Id => "MapPins";
        public override string DisplayName => "Map pins";

        public override string Description =>
            "Show benches and fast travel on the map. Does not reveal unexplored areas.";

        protected override void BindSettings(ConfigFile config)
        {
            _benches = config.Bind(Id, "Benches", true,
                "Pin every bench you have already discovered.");

            _fastTravel = config.Bind(Id, "FastTravel", true,
                "Pin fast-travel points you have discovered (bell stations and tubes).");

            _shops = config.Bind(Id, "Shops", false,
                "Pin shops and spas you have discovered.");

            _cocoon = config.Bind(Id, "DeathCocoon", true,
                "Pin your death cocoon, so you can find the rosaries you dropped.");

            AddRow(new BoolRow("Benches", "Benches you have found.", _benches));
            AddRow(new BoolRow("Fast travel", "Bell stations and tubes you have found.", _fastTravel));
            AddRow(new BoolRow("Shops and spas", "Shops and spas you have found.", _shops));
            AddRow(new BoolRow("Death cocoon", "Where you dropped your rosaries.", _cocoon));
        }

        protected override TweakStatus Apply(Harmony harmony)
        {
            _instance = this;

            var getBool = Resolve(HookTargets.GetBool, out var error);
            if (getBool == null) return TweakStatus.Unavailable(error);

            var hasAnyPin = Resolve(HookTargets.HasAnyPin, out error);
            if (hasAnyPin == null) return TweakStatus.Unavailable(error);

            harmony.Patch(getBool, postfix: new HarmonyMethod(
                typeof(MapPinsModule), nameof(GetBoolPostfix)));

            harmony.Patch(hasAnyPin, postfix: new HarmonyMethod(
                typeof(MapPinsModule), nameof(HasAnyPinPostfix)));

            return TweakStatus.Active;
        }

        /// <summary>Which pin flags we are currently claiming to own.</summary>
        private IEnumerable<string> EnabledFlags()
        {
            if (_benches.Value) yield return BenchFlag;

            if (_fastTravel.Value)
            {
                yield return StagFlag;
                yield return TubeFlag;
            }

            if (_shops.Value)
            {
                yield return ShopFlag;
                yield return SpaFlag;
            }

            if (_cocoon.Value) yield return CocoonFlag;
        }

        private bool Grants(string flag)
        {
            foreach (var enabled in EnabledFlags())
            {
                if (enabled == flag) return true;
            }

            return false;
        }

        private static void GetBoolPostfix(string boolName, ref bool __result)
        {
            var self = _instance;
            if (self == null || !self.IsOn || __result) return;
            if (string.IsNullOrEmpty(boolName)) return;
            if (!self.Grants(boolName)) return;

            __result = true;
            self.MarkFired();
        }

        private static void HasAnyPinPostfix(ref bool __result)
        {
            var self = _instance;
            if (self == null || !self.IsOn || __result) return;

            foreach (var unused in self.EnabledFlags())
            {
                __result = true;
                return;
            }
        }
    }
}
