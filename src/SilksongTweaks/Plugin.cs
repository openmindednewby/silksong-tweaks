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
        private bool _uiBroken;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            try
            {
                _toggleKey = Config.Bind("General", "ToggleKey", KeyCode.F8,
                    "Opens and closes the Silksong Tweaks panel.");

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

            if (Input.GetKeyDown(_toggleKey.Value))
            {
                _window.Visible = !_window.Visible;
            }
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
