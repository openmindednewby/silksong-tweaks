using System.Collections;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SilksongTweaks.Modules
{
    /// <summary>
    /// After respawning at a bench, returns Hornet to where she died.
    ///
    /// HOW THIS WORKS, AND WHY THE OBVIOUS VERSION DOES NOT
    /// -----------------------------------------------------
    /// BeginSceneTransition needs an EntryGateName naming a TransitionPoint that EXISTS in the
    /// destination scene. Passing an empty string loads the scene but leaves the hero with no
    /// entry point, so the arrival sequence never completes and the camera fade never lifts:
    /// a permanent black screen with no control. That was v1 of this module.
    ///
    /// The game never invents a gate either. HeroController.HazardRespawn first tries to
    /// reposition inside the current scene via TryFindGroundPoint, and only crosses scenes by
    /// locating a real TransitionPoint and using its targetScene + entryPoint.
    ///
    /// So we do the same: while dying we are still IN the death scene, so we record one of its
    /// real gates. On the way back we enter through that gate — a completely ordinary transition
    /// the game knows how to finish — and only then move Hornet to the exact spot, snapped to
    /// solid ground with the game's own TryFindGroundPoint.
    ///
    /// If no gate can be recorded, we do not transition at all. Staying at the bench is a poor
    /// outcome; a black screen is an unrecoverable one.
    /// </summary>
    public sealed class ReturnToDeathModule : ModuleBase
    {
        private const float MinCountdown = 0f;
        private const float MaxCountdown = 15f;
        private const float ArrivalTimeout = 15f;
        private const float SettleSeconds = 0.35f;

        private static ReturnToDeathModule _instance;

        private ConfigEntry<float> _countdown;
        private ConfigEntry<KeyCode> _cancelKey;

        private string _deathScene;
        private string _deathGate;
        private Vector3 _deathPosition;
        private bool _hasRecord;
        private bool _returning;

        public override string Id => "ReturnToDeath";
        public override string DisplayName => "Return to death spot";
        public override string Description => "Travel back to where you died after respawning.";

        /// <summary>Seconds left on the countdown, or 0 when idle. Drives the on-screen prompt.</summary>
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

            var die = Resolve(HookTargets.Die, out var error);
            if (die == null) return TweakStatus.Unavailable(error);

            var respawned = Resolve(HookTargets.HeroRespawned, out error);
            if (respawned == null) return TweakStatus.Unavailable(error);

            if (Resolve(HookTargets.BeginSceneTransition, out error) == null)
                return TweakStatus.Unavailable(error);

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
            self._deathGate = FindGateInCurrentScene();
            self._hasRecord = !string.IsNullOrEmpty(self._deathGate);

            if (!self._hasRecord)
            {
                Plugin.Log.LogWarning(
                    $"[ReturnToDeath] no transition point found in '{self._deathScene}'. " +
                    "Staying at the bench rather than risking a transition with no entry gate.");
                return;
            }

            Plugin.Log.LogInfo(
                $"[ReturnToDeath] recorded death in '{self._deathScene}' at {self._deathPosition} " +
                $"via gate '{self._deathGate}'");
        }

        /// <summary>
        /// A gate we can legitimately arrive through. Doors are skipped where possible: arriving
        /// through one plays an entry animation that fights an immediate reposition.
        /// </summary>
        private static string FindGateInCurrentScene()
        {
            var points = TransitionPoint.TransitionPoints;
            if (points == null || points.Count == 0) return null;

            string fallback = null;

            foreach (var point in points)
            {
                if (point == null) continue;
                if (string.IsNullOrEmpty(point.name)) continue;

                if (!point.isADoor) return point.name;
                if (fallback == null) fallback = point.name;
            }

            return fallback;
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

            CountdownRemaining = 0f;
            yield return Travel();
            Finish();
        }

        private IEnumerator Travel()
        {
            var gm = GameManager.instance;
            if (gm == null || string.IsNullOrEmpty(_deathScene) || string.IsNullOrEmpty(_deathGate))
                yield break;

            // NEVER start a transition while the game is still running its own respawn. With the
            // countdown at 0 this routine begins on the same frame as HeroRespawned, and two
            // overlapping scene transitions leave the camera faded out with no control — an
            // unrecoverable black screen. Observed in play, not in theory.
            yield return WaitUntilRespawnFinished();

            if (gm.GetSceneNameString() != _deathScene)
            {
                Plugin.Log.LogInfo($"[ReturnToDeath] entering '{_deathScene}' via gate '{_deathGate}'");

                gm.BeginSceneTransition(new GameManager.SceneLoadInfo
                {
                    SceneName = _deathScene,
                    EntryGateName = _deathGate,
                });

                var waited = 0f;
                while (waited < ArrivalTimeout)
                {
                    var current = GameManager.instance;
                    if (current != null && current.GetSceneNameString() == _deathScene
                        && HeroController.instance != null)
                    {
                        break;
                    }

                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (waited >= ArrivalTimeout)
                {
                    Plugin.Log.LogWarning("[ReturnToDeath] timed out waiting for the scene; leaving you where you are");
                    yield break;
                }

                // Let the arrival sequence finish before moving anything, or the game's own
                // entry logic will fight the reposition and can strand the camera.
                yield return new WaitForSeconds(SettleSeconds);
            }

            PlaceHeroAtDeathSpot();
            MarkFired();
        }

        /// <summary>
        /// Blocks until the game's own respawn has finished and the hero is real and controllable.
        /// This is what makes a zero-second countdown safe: "immediately" means as soon as the
        /// game is ready, not on the same frame the respawn started.
        /// </summary>
        private static IEnumerator WaitUntilRespawnFinished()
        {
            var waited = 0f;

            while (waited < ArrivalTimeout)
            {
                var gm = GameManager.instance;
                var hero = HeroController.instance;

                var busy = gm == null || hero == null || gm.RespawningHero;
                if (!busy) break;

                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            // A short settle even once the flag clears: the fade-in and hero-entry animation run
            // slightly past it, and moving Hornet mid-animation strands the camera.
            yield return new WaitForSeconds(SettleSeconds);
        }

        private void PlaceHeroAtDeathSpot()
        {
            var hero = HeroController.instance;
            if (hero == null) return;

            // Snap to real ground using the game's own probe so we never land inside geometry.
            // It is non-public, so this is reflective and OPTIONAL: if the game renames it we
            // still place Hornet at the recorded spot, which is somewhere she demonstrably stood.
            var probe = AccessTools.Method(typeof(HeroController), "TryFindGroundPoint");
            if (probe != null)
            {
                var args = new object[] { default(Vector2), (Vector2)_deathPosition, true };
                if (probe.Invoke(hero, args) is bool found && found)
                {
                    var ground = (Vector2)args[0];
                    hero.transform.position = new Vector3(ground.x, ground.y, hero.transform.position.z);
                    Plugin.Log.LogInfo($"[ReturnToDeath] placed at ground point {ground}");
                    return;
                }
            }

            hero.transform.position = _deathPosition;
            Plugin.Log.LogInfo($"[ReturnToDeath] placed at recorded position {_deathPosition}");
        }

        private void Finish()
        {
            CountdownRemaining = 0f;
            _returning = false;
            _hasRecord = false;
        }
    }
}
