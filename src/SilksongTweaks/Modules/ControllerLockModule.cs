using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using InControl;

namespace SilksongTweaks.Modules
{
    /// <summary>
    /// Restricts this game instance to a single gamepad.
    ///
    /// Local co-op is played by running Silksong twice side by side. Both copies read every
    /// connected pad, so without this each Hornet answers to both controllers. Setting a
    /// different JoystickIndex in each copy gives one pad one Hornet.
    ///
    /// WHY THIS IS NOT LEGACY UNITY INPUT: Silksong does not read gamepads through
    /// UnityEngine.Input, so KeyCode.Joystick1Button0 and "Joystick1Axis1" have nothing to
    /// filter. It uses InControl (compiled into Assembly-CSharp), whose PlayerActionSet.Update
    /// resolves one device per frame as <c>Device ?? FindActiveDevice()</c> and feeds it to every
    /// action. FindActiveDevice, in turn, only considers devices present in IncludeDevices once
    /// that list is non-empty. So a one-entry IncludeDevices is a hard restriction expressed in
    /// InControl's own supported API — this filters existing input rather than reimplementing it.
    ///
    /// THE KEYBOARD IS UNAFFECTED, by construction rather than by care: gamepad bindings are
    /// DeviceBindingSource, which reads the device it is handed, while keyboard bindings are
    /// KeyBindingSource, whose GetState ignores the device argument entirely and reads the
    /// keyboard directly. Filtering devices therefore cannot reach the keyboard, so one player
    /// can always fall back to it.
    ///
    /// The patch is placed on the PlayerActionSet base method, which no subclass overrides, so a
    /// single hook covers every action set the game creates — gameplay, menus and pre-menu alike.
    /// </summary>
    public sealed class ControllerLockModule : ModuleBase
    {
        /// <summary>Do not filter at all: the vanilla behaviour, and the default.</summary>
        private const int AllJoysticks = 0;

        /// <summary>Per-process override, so two instances can share one install folder.</summary>
        private const string JoystickEnvVar = "SILKSONG_JOYSTICK";

        /// <summary>Matches InControl's practical device count; two is all local co-op needs.</summary>
        private const int MaxJoystickIndex = 4;

        private static ControllerLockModule _instance;

        /// <summary>
        /// Exactly the action sets we restricted. Releasing only these means turning the tweak
        /// off can never wipe an IncludeDevices list that belongs to somebody else. The game
        /// itself never writes IncludeDevices (verified: zero IL references), so in practice the
        /// only other writer would be another mod.
        /// </summary>
        private static readonly HashSet<PlayerActionSet> Restricted = new HashSet<PlayerActionSet>();

        private ConfigEntry<int> _joystickIndex;

        public override string Id => "ControllerLock";
        public override string DisplayName => "Controller lock";

        public override string Description =>
            "Limit this game instance to one controller, for two-instance local co-op.";

        protected override void BindSettings(ConfigFile config)
        {
            // Default 0 deliberately: installing this module must change nothing for the people
            // already running the mod single-player.
            _joystickIndex = config.Bind(Id, "JoystickIndex", AllJoysticks,
                new ConfigDescription(
                    "0 = off: every controller drives this instance, exactly as vanilla. " +
                    "1 = only the first controller, 2 = only the second. " +
                    "The keyboard is never filtered, whatever this is set to.",
                    new AcceptableValueRange<int>(AllJoysticks, MaxJoystickIndex)));

            AddRow(new IntRow("Joystick", "0 = all controllers. 1 = first pad, 2 = second pad.",
                _joystickIndex, AllJoysticks, MaxJoystickIndex));
        }

        /// <summary>
        /// The joystick this instance is locked to, with the environment variable winning.
        ///
        /// This is what lets local co-op run TWO INSTANCES FROM ONE INSTALL FOLDER. BepInEx
        /// config is per-install, so two processes sharing a folder read the same JoystickIndex
        /// and would lock onto the same pad. An environment variable is per-process, so the
        /// launcher sets SILKSONG_JOYSTICK=1 for one window and 2 for the other, and neither
        /// touches the config file. The alternative was copying the whole 7.7 GB game folder.
        ///
        /// Config remains the default so single-player users are unaffected and the panel still
        /// works normally when nothing sets the variable.
        /// </summary>
        private int EffectiveIndex
        {
            get
            {
                var raw = System.Environment.GetEnvironmentVariable(JoystickEnvVar);

                if (!string.IsNullOrEmpty(raw)
                    && int.TryParse(raw, out var parsed)
                    && parsed >= AllJoysticks
                    && parsed <= MaxJoystickIndex)
                {
                    return parsed;
                }

                return _joystickIndex != null ? _joystickIndex.Value : AllJoysticks;
            }
        }

        protected override TweakStatus Apply(Harmony harmony)
        {
            _instance = this;

            // Both targets are checked so a game update that moves either one produces a greyed
            // out row naming the missing member, rather than a lock that silently stops locking.
            // The device list is a FIELD, which ModuleBase.Resolve cannot look up — it handles
            // methods and property getters only — so it is resolved here instead.
            var devicesError = MissingFieldError(HookTargets.InputManagerDevices);
            if (devicesError != null) return TweakStatus.Unavailable(devicesError);

            var update = Resolve(HookTargets.PlayerActionSetUpdate, out var updateError);
            if (update == null) return TweakStatus.Unavailable(updateError);

            harmony.Patch(update, prefix: new HarmonyMethod(
                typeof(ControllerLockModule), nameof(UpdatePrefix)));

            return TweakStatus.Active;
        }

        /// <summary>
        /// Null when the field is present, or a message naming what is missing when it is not.
        /// </summary>
        private static string MissingFieldError(HookTarget target)
        {
            var type = AccessTools.TypeByName(target.DeclaringType);
            if (type == null) return $"type '{target.DeclaringType}' not found";

            return AccessTools.Field(type, target.Member) == null
                ? $"{target.DeclaringType}.{target.Member} not found"
                : null;
        }

        /// <summary>
        /// Runs immediately before an action set picks the device it will read this frame, which
        /// is the only moment at which the choice can still be changed.
        ///
        /// Re-asserted every frame on purpose: pads are attached and detached at runtime, and the
        /// panel can toggle this tweak mid-game. Both are cheap — the work below is two reference
        /// comparisons unless something actually changed.
        /// </summary>
        /// <summary>
        /// While true, NO gamepad input reaches the game at all.
        ///
        /// Set by the panel while it is open. Without this, pressing A to flip a toggle also makes
        /// Hornet attack, and the d-pad both moves the cursor and picks items in the game's own
        /// menus — you end up editing two things at once. Freezing time does not help, because
        /// Update still runs and InControl still delivers input.
        ///
        /// The panel itself is unaffected: it reads legacy UnityEngine.Input, while this blocks
        /// InControl. Two separate pipelines, which is exactly why one can be muted without
        /// deafening the other. The keyboard also still reaches the game, since InControl's
        /// keyboard bindings ignore the device — one reason Esc still closes things normally.
        /// </summary>
        public static bool SuppressGameInput { get; set; }

        private static void UpdatePrefix(PlayerActionSet __instance)
        {
            var self = _instance;
            if (self == null || __instance == null) return;

            // Checked before the enable toggle: suppression is the panel protecting itself, not a
            // user-facing tweak, so it must work even with Controller lock switched off.
            if (SuppressGameInput)
            {
                Mute(__instance);
                return;
            }

            var index = self.IsOn ? self.EffectiveIndex : AllJoysticks;
            if (index == AllJoysticks)
            {
                Release(__instance);
                return;
            }

            var device = DeviceAt(index);
            if (device == null)
            {
                // The chosen pad is not plugged in. An empty-but-filtered set would deaden the
                // controls completely, which reads to a player as the game freezing, so we fall
                // back to unfiltered input instead of to no input.
                Release(__instance);
                return;
            }

            Restrict(__instance, device);
        }

        /// <summary>
        /// The index-th attached device, 1-based, or null when that pad is not present.
        /// InputManager.Devices is InControl's attach-ordered list, so index 1 is the pad that
        /// connected first — the same order the player sees.
        /// </summary>
        private static InputDevice DeviceAt(int oneBasedIndex)
        {
            var devices = InputManager.Devices;
            if (devices == null) return null;

            var zeroBased = oneBasedIndex - 1;
            if (zeroBased < 0 || zeroBased >= devices.Count) return null;

            return devices[zeroBased];
        }

        /// <summary>
        /// Points the action set at InControl's Null device, which is never attached, so
        /// FindActiveDevice resolves nothing and every gamepad action reads as unpressed.
        /// Deliberately does not MarkFired: this is not the tweak doing its job.
        /// </summary>
        private static void Mute(PlayerActionSet set)
        {
            var include = set.IncludeDevices;
            if (include == null) return;

            if (include.Count == 1 && ReferenceEquals(include[0], InputDevice.Null)) return;

            include.Clear();
            include.Add(InputDevice.Null);
            Restricted.Add(set);
        }

        private static void Restrict(PlayerActionSet set, InputDevice device)
        {
            var include = set.IncludeDevices;
            if (include == null) return;

            var alreadyCorrect = include.Count == 1 && ReferenceEquals(include[0], device);
            if (alreadyCorrect) return;

            include.Clear();
            include.Add(device);
            Restricted.Add(set);
            _instance?.MarkFired();
        }

        /// <summary>
        /// Hand the set back its full choice of devices. Patches are never removed at runtime —
        /// Harmony unpatching is a known source of instability — so switching the tweak off has
        /// to be undone here instead.
        /// </summary>
        private static void Release(PlayerActionSet set)
        {
            if (!Restricted.Remove(set)) return;

            var include = set.IncludeDevices;
            if (include == null || include.Count == 0) return;

            include.Clear();
            _instance?.MarkFired();
        }
    }
}
