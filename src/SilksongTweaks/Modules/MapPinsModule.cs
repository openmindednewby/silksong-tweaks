using BepInEx.Configuration;
using HarmonyLib;

namespace SilksongTweaks.Modules
{
    /// <summary>
    /// Shows map pins — benches, fast travel, shops — without buying them from the cartographer.
    ///
    /// THIS DOES NOT REVEAL THE MAP, and that is guaranteed by the game's own code rather than by
    /// our care. MapPin.CanBeActive reads like this:
    ///
    ///     if (!IsActive) return false;                      &lt;- the pin-ownership gate, we force this
    ///     if (!parentScene) return true;
    ///     if (parentScene.IsMapped) return true;            &lt;- room discovery, untouched
    ///     if (parentScene.InitialState) return parentScene.IsVisited;
    ///     return false;
    ///
    /// The discovery checks run AFTER the gate we force, so a pin in a room you have never
    /// entered still fails on IsMapped/IsVisited. You see pins where you have been, and nowhere
    /// else. PlayerData.mapAllRooms — the flag that would actually reveal the map — is never
    /// touched.
    ///
    /// WHY THE FIRST ATTEMPT FAILED, recorded so it is not repeated: it postfixed
    /// PlayerData.GetBool("hasPinBench"), on the reasoning that the pin flags had no direct IL
    /// readers so the map must read them by name. That inference was worthless — GetBool resolves
    /// fields by REFLECTION, which leaves no IL trace either way, so the absence of callers proved
    /// nothing. The module hooked cleanly, reported ACTIVE, and did nothing at all.
    ///
    /// ONE PIN TYPE, NOT SIX: isActive carries no pin category, so this cannot separate benches
    /// from shops. Rather than offer per-type toggles that quietly do nothing, there is one
    /// switch. Read-side only, so the save is untouched and the real pins can still be bought.
    /// </summary>
    public sealed class MapPinsModule : ModuleBase
    {
        private static MapPinsModule _instance;

        private ConfigEntry<bool> _showPins;

        public override string Id => "MapPins";
        public override string DisplayName => "Map pins";

        public override string Description =>
            "Show pins on places you have already explored. Does not reveal new areas.";

        protected override void BindSettings(ConfigFile config)
        {
            _showPins = config.Bind(Id, "ShowPins", true,
                "Show benches, fast travel and other pins on rooms you have already mapped or " +
                "visited, without buying the pins. Unexplored rooms stay blank.");

            AddRow(new BoolRow("Show pins", "Benches, fast travel and shops you have found.", _showPins));
        }

        protected override TweakStatus Apply(Harmony harmony)
        {
            _instance = this;

            var isActive = Resolve(HookTargets.MapPinIsActive, out var error);
            if (isActive == null) return TweakStatus.Unavailable(error);

            var hasAnyPin = Resolve(HookTargets.HasAnyPin, out error);
            if (hasAnyPin == null) return TweakStatus.Unavailable(error);

            harmony.Patch(isActive, postfix: new HarmonyMethod(
                typeof(MapPinsModule), nameof(IsActivePostfix)));

            // HasAnyPin reads the pin FIELDS directly rather than going through anything we hook,
            // so without this the map can honour individual pins while still believing you own
            // none — which hides the pin legend.
            harmony.Patch(hasAnyPin, postfix: new HarmonyMethod(
                typeof(MapPinsModule), nameof(HasAnyPinPostfix)));

            return TweakStatus.Active;
        }

        private bool Enabled => IsOn && _showPins != null && _showPins.Value;

        private static void IsActivePostfix(ref bool __result)
        {
            var self = _instance;
            if (self == null || __result || !self.Enabled) return;

            __result = true;
            self.MarkFired();
        }

        private static void HasAnyPinPostfix(ref bool __result)
        {
            var self = _instance;
            if (self == null || __result || !self.Enabled) return;

            __result = true;
        }
    }
}
