using System;
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SilksongTweaks.Modules;
using SilksongTweaks.Ui;
using UnityEngine;

namespace SilksongTweaks
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.dloizides.silksongtweaks";
        public const string PluginName = "Silksong Tweaks";
        public const string PluginVersion = "1.0.0";

        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }

        private ModuleRegistry _registry;
        private TweakWindow _window;
        private ConfigEntry<KeyCode> _toggleKey;
        private ConfigEntry<KeyCode> _gamepadToggleButton;
        private ConfigEntry<bool> _freezeWhileOpen;
        private bool _uiBroken;
        private bool _wasVisible;
        private float _timeScaleBeforeOpen = 1f;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            try
            {
                _toggleKey = Config.Bind("General", "ToggleKey", KeyCode.F8,
                    "Opens and closes the Silksong Tweaks panel.");

                // JoystickButton6 is Back/View on an Xbox pad and Share on a DualShock. Chosen
                // because it is not bound to anything in normal play, so opening the panel never
                // costs you a jump or an attack.
                _gamepadToggleButton = Config.Bind("General", "GamepadToggleButton",
                    KeyCode.JoystickButton6,
                    "Gamepad button that opens the panel. JoystickButton6 is Back/View/Share.");

                _freezeWhileOpen = Config.Bind("General", "FreezeGameWhileOpen", true,
                    "Pause the game while the panel is open, so navigating it does not also " +
                    "move Hornet. Turn off if it conflicts with anything.");

                _registry = new ModuleRegistry(Log);
                _registry.Add(new ReturnToDeathModule());
                _registry.Add(new KeepRosariesModule());
                _registry.Add(new MaxHealthModule());
                _registry.Add(new DamageTakenModule());

                _registry.ApplyAll(Config, new Harmony(PluginGuid));
                _window = new TweakWindow(_registry);

                Log.LogInfo($"{PluginName} {PluginVersion} ready. Press {_toggleKey.Value} for the panel.");
            }
            catch (Exception ex)
            {
                // Awake must never throw: a dead plugin with no panel is strictly worse than a
                // panel that opens and explains what went wrong.
                Log.LogError($"startup failed: {ex}");
            }
        }

        /// <summary>Lets modules run coroutines without each becoming a MonoBehaviour.</summary>
        public Coroutine Run(IEnumerator routine) => StartCoroutine(routine);

        private void Update()
        {
            if (_window == null || _toggleKey == null) return;

            if (Input.GetKeyDown(_toggleKey.Value)
                || (_gamepadToggleButton != null && Input.GetKeyDown(_gamepadToggleButton.Value)))
            {
                _window.Visible = !_window.Visible;
            }

            // Unscaled, so navigation keeps its repeat rate even while the game is frozen.
            _window.HandleInput(Time.unscaledDeltaTime);

            ApplyFreeze();
        }

        /// <summary>
        /// Freezes gameplay while the panel is open so stick input navigates the menu instead of
        /// also walking Hornet into a hazard. The previous timeScale is restored rather than
        /// assumed to be 1, so we never clobber a pause the game set itself.
        /// </summary>
        private void ApplyFreeze()
        {
            if (_freezeWhileOpen == null || !_freezeWhileOpen.Value)
            {
                _wasVisible = _window.Visible;
                return;
            }

            if (_window.Visible == _wasVisible) return;
            _wasVisible = _window.Visible;

            if (_window.Visible)
            {
                _timeScaleBeforeOpen = Time.timeScale;
                Time.timeScale = 0f;
                return;
            }

            Time.timeScale = _timeScaleBeforeOpen <= 0f ? 1f : _timeScaleBeforeOpen;
        }

        private void OnDestroy()
        {
            // Never leave the game frozen if the plugin is torn down while the panel is open.
            if (_wasVisible && Time.timeScale == 0f) Time.timeScale = 1f;
        }

        private void OnGUI()
        {
            if (_window == null || _uiBroken) return;

            try
            {
                _window.Draw();
            }
            catch (Exception ex)
            {
                // An exception here fires every frame: thousands of log lines a minute and a
                // visible framerate collapse that reads as the GAME breaking. Fail once, loudly,
                // then stay quiet — the tweaks themselves keep working without the panel.
                _uiBroken = true;
                Log.LogError($"UI disabled after a draw error: {ex}");
            }
        }
    }
}
