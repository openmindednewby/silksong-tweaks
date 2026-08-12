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

        /// <summary>Caps the diagnostic spam: the map can be opened many times per session.</summary>
        private static int _reportsLeft = 8;

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

            // Forcing the GETTER alone was not enough, and the reason is worth keeping: MapPin
            // only re-evaluates visibility inside ApplyState, which runs on Start, on CycleState,
            // and when IsActive is SET — never merely because the getter would now answer
            // differently. So the map was asking once, before our answer could matter, and never
            // asking again. GameMap.EnableUnlockedAreas is what actually sets IsActive per pin, so
            // we re-activate them there, which pushes each pin through ApplyState and a fresh
            // CanBeActive — including its untouched IsMapped / IsVisited discovery checks.
            var enableAreas = Resolve(HookTargets.EnableUnlockedAreas, out error);
            if (enableAreas == null) return TweakStatus.Unavailable(error);

            harmony.Patch(enableAreas, postfix: new HarmonyMethod(
                typeof(MapPinsModule), nameof(EnableUnlockedAreasPostfix)));

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

        /// <summary>
        /// Re-activates every pin under the map after the game has finished deciding which ones
        /// you own. Setting IsActive is what makes a pin re-run ApplyState and redraw, so this is
        /// a write to the pin COMPONENT — not to PlayerData, so the save is still untouched.
        /// </summary>
        private static void EnableUnlockedAreasPostfix(GameMap __instance)
        {
            var self = _instance;
            if (self == null || __instance == null || !self.Enabled) return;

            try
            {
                // Include inactive children: a pin you do not own may be disabled in the hierarchy.
                var pins = __instance.GetComponentsInChildren<MapPin>(true);
                if (pins == null || pins.Length == 0) return;

                var activated = 0;
                foreach (var pin in pins)
                {
                    if (pin == null || pin.IsActive) continue;
                    pin.IsActive = true;
                    activated++;
                }

                self.MarkFired();

                // Logged UNCONDITIONALLY, and that matters: the previous version only logged when
                // it activated something, so "found zero pins" and "was never called" produced
                // identical silence — the two cases needing completely different fixes.
                if (_reportsLeft > 0)
                {
                    _reportsLeft--;
                    Plugin.Log.LogInfo(
                        $"[MapPins] EnableUnlockedAreas ran: found {pins.Length} pin(s), " +
                        $"activated {activated}");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"[MapPins] could not activate pins: {ex.Message}");
            }
        }

        private static void HasAnyPinPostfix(ref bool __result)
        {
            var self = _instance;
            if (self == null || __result || !self.Enabled) return;

            __result = true;
        }
    }
}
