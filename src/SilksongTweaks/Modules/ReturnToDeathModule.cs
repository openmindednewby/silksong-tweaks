using System.Collections;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SilksongTweaks.Modules
{
    /// <summary>
    /// After respawning at a bench, returns Hornet to where she died.
    ///
    /// Death location is captured in a prefix on HeroController.Die. The return itself runs after
    /// HeroRespawned, on a cancellable countdown, and uses the game's own scene transition rather
    /// than writing a position directly — a raw transform write can drop you through geometry if
    /// the target scene is not loaded.
    /// </summary>
    public sealed class ReturnToDeathModule : ModuleBase
    {
        private const float MinCountdown = 0f;
        private const float MaxCountdown = 15f;

        private static ReturnToDeathModule _instance;

        private ConfigEntry<float> _countdown;
        private ConfigEntry<KeyCode> _cancelKey;

        private string _deathScene;
        private Vector3 _deathPosition;
        private bool _hasRecord;
        private bool _returning;

        public override string Id => "ReturnToDeath";
        public override string DisplayName => "Return to death spot";
        public override string Description => "Teleport back to where you died after respawning.";

        /// <summary>Seconds remaining on the countdown, or 0 when idle. Drives the on-screen prompt.</summary>
        public float CountdownRemaining { get; private set; }

        public KeyCode CancelKey => _cancelKey != null ? _cancelKey.Value : KeyCode.N;

        protected override void BindSettings(ConfigFile config)
        {
            _countdown = config.Bind(Id, "CountdownSeconds", 5f,
                new ConfigDescription(
                    "Seconds before the return happens, giving you time to cancel.",
                    new AcceptableValueRange<float>(MinCountdown, MaxCountdown)));

            _cancelKey = config.Bind(Id, "CancelKey", KeyCode.N,
                "Press during the countdown to stay at the bench.");

            AddRow(new FloatRow("Countdown", "Seconds before returning.",
                _countdown, MinCountdown, MaxCountdown, "0.0s"));
            AddRow(new KeyRow("Cancel key", "Press during the countdown to stay put.", _cancelKey));
        }

        protected override TweakStatus Apply(Harmony harmony)
        {
            _instance = this;

            var die = Resolve(HookTargets.Die, out var err1);
            if (die == null) return TweakStatus.Unavailable(err1);

            var respawned = Resolve(HookTargets.HeroRespawned, out var err2);
            if (respawned == null) return TweakStatus.Unavailable(err2);

            if (Resolve(HookTargets.BeginSceneTransition, out var err3) == null)
                return TweakStatus.Unavailable(err3);

            harmony.Patch(die, prefix: new HarmonyMethod(
                typeof(ReturnToDeathModule), nameof(DiePrefix)));

            harmony.Patch(respawned, postfix: new HarmonyMethod(
                typeof(ReturnToDeathModule), nameof(HeroRespawnedPostfix)));

            return TweakStatus.Active;
        }

        private static void DiePrefix()
        {
            var self = _instance;
            if (self == null || !self.IsOn) return;

            var hero = HeroController.instance;
            var gm = GameManager.instance;
            if (hero == null || gm == null) return;

            self._deathScene = gm.GetSceneNameString();
            self._deathPosition = hero.transform.position;
            self._hasRecord = true;

            Plugin.Log.LogInfo(
                $"[ReturnToDeath] recorded death at {self._deathScene} {self._deathPosition}");
        }

        private static void HeroRespawnedPostfix()
        {
            var self = _instance;
            if (self == null || !self.IsOn || !self._hasRecord || self._returning) return;

            Plugin.Instance.Run(self.ReturnRoutine());
        }

        private IEnumerator ReturnRoutine()
        {
            _returning = true;
            CountdownRemaining = _countdown.Value;

            while (CountdownRemaining > 0f)
            {
                if (Input.GetKeyDown(CancelKey))
                {
                    Plugin.Log.LogInfo("[ReturnToDeath] cancelled by player");
                    Finish();
                    yield break;
                }

                CountdownRemaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            Travel();
            Finish();
        }

        private void Travel()
        {
            var gm = GameManager.instance;
            if (gm == null || string.IsNullOrEmpty(_deathScene)) return;

            Plugin.Log.LogInfo($"[ReturnToDeath] returning to {_deathScene} {_deathPosition}");

            var hero = HeroController.instance;
            if (hero != null)
            {
                // Seed the hazard respawn point so the hero lands at the recorded spot rather
                // than at the scene's default gate.
                hero.SetHazardRespawn(_deathPosition, true);
            }

            if (gm.GetSceneNameString() == _deathScene)
            {
                if (hero != null) hero.transform.position = _deathPosition;
                MarkFired();
                return;
            }

            gm.BeginSceneTransition(new GameManager.SceneLoadInfo
            {
                SceneName = _deathScene,
                EntryGateName = string.Empty,
                EntryDelay = 0f,
                PreventCameraFadeOut = false,
                WaitForSceneTransitionCameraFade = true,
                AlwaysUnloadUnusedAssets = false,
            });

            MarkFired();
        }

        private void Finish()
        {
            CountdownRemaining = 0f;
            _returning = false;
            _hasRecord = false;
        }
    }
}
