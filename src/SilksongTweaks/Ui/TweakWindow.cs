using System.Collections.Generic;
using SilksongTweaks.Modules;
using UnityEngine;

namespace SilksongTweaks.Ui
{
    /// <summary>
    /// The F8 panel. Renders purely from what modules expose — name, settings, status — so
    /// adding a tweak never touches this file.
    /// </summary>
    public sealed class TweakWindow
    {
        private const int WindowId = 0x51_4B_53; // arbitrary, just needs to be stable
        private static readonly Vector2 Size = new Vector2(430f, 560f);

        private readonly ModuleRegistry _registry;
        private readonly Theme _theme = new Theme();
        private readonly Dictionary<string, bool> _listening = new Dictionary<string, bool>();
        private IReadOnlyList<string> _conflicts;

        private Rect _rect = new Rect(60f, 60f, Size.x, Size.y);
        private Vector2 _scroll;
        private bool _showCredits;

        public TweakWindow(ModuleRegistry registry) => _registry = registry;

        public bool Visible { get; set; }

        public void Draw()
        {
            _theme.EnsureBuilt();

            DrawCountdownToast();

            if (!Visible) return;

            if (_conflicts == null) _conflicts = Conflicts.Detect();

            _rect = GUILayout.Window(WindowId, _rect, DrawBody,
                $"  SILKSONG TWEAKS   ·   {_registry.ActiveCount}/{_registry.Modules.Count} active",
                _theme.Window, GUILayout.Width(Size.x), GUILayout.Height(Size.y));
        }

        /// <summary>
        /// Shown even when the panel is closed: during a return countdown you need to know it is
        /// happening and how to stop it, without opening a menu mid-respawn.
        /// </summary>
        private void DrawCountdownToast()
        {
            ReturnToDeathModule ret = null;
            foreach (var m in _registry.Modules)
            {
                ret = m as ReturnToDeathModule;
                if (ret != null) break;
            }

            if (ret == null || ret.CountdownRemaining <= 0f) return;

            var text = $"Returning to where you died in {ret.CountdownRemaining:0.0}s" +
                       $"\nPress {ret.CancelKey} to stay here";

            var size = _theme.Toast.CalcSize(new GUIContent(text));
            var rect = new Rect((Screen.width - size.x) * 0.5f, Screen.height * 0.14f, size.x, size.y);
            GUI.Label(rect, text, _theme.Toast);
        }

        private void DrawBody(int id)
        {
            if (_conflicts != null && _conflicts.Count > 0) DrawConflictWarning();

            _scroll = GUILayout.BeginScrollView(_scroll);

            foreach (var module in _registry.Modules) DrawModule(module);

            GUILayout.Space(8f);
            DrawCredits();

            GUILayout.EndScrollView();

            GUI.DragWindow(new Rect(0f, 0f, Size.x, 22f));
        }

        private void DrawConflictWarning()
        {
            var previous = _theme.SectionDesc.normal.textColor;
            _theme.SectionDesc.normal.textColor = Theme.Bad;
            GUILayout.Label(
                "CONFLICT: these mods do the same job and will fight this one.\n  · " +
                string.Join("\n  · ", ArrayOf(_conflicts)) +
                "\nRemove them from BepInEx/plugins.", _theme.SectionDesc);
            _theme.SectionDesc.normal.textColor = previous;
            GUILayout.Space(6f);
        }

        private void DrawModule(ITweakModule module)
        {
            GUILayout.Label(module.DisplayName, _theme.SectionTitle);
            Widgets.StatusBadge(_theme, module.Status, module.LastFiredUtcTicks);

            if (module.Status.State == TweakState.Unavailable)
            {
                GUILayout.Label(module.Description, _theme.SectionDesc);
                GUILayout.Space(6f);
                return;
            }

            foreach (var row in module.Settings) DrawRow(module, row);
            GUILayout.Space(6f);
        }

        private void DrawRow(ITweakModule module, ISettingRow row)
        {
            var boolRow = row as BoolRow;
            if (boolRow != null)
            {
                var v = boolRow.Entry.Value;
                Widgets.Toggle(_theme, boolRow.Label, boolRow.Tooltip, ref v);
                if (v != boolRow.Entry.Value) boolRow.Entry.Value = v;
                return;
            }

            var intRow = row as IntRow;
            if (intRow != null)
            {
                var v = intRow.Entry.Value;
                Widgets.IntSlider(_theme, intRow.Label, intRow.Tooltip, ref v, intRow.Min, intRow.Max);
                if (v != intRow.Entry.Value) intRow.Entry.Value = v;
                return;
            }

            var floatRow = row as FloatRow;
            if (floatRow != null)
            {
                var v = floatRow.Entry.Value;
                Widgets.FloatSlider(_theme, floatRow.Label, floatRow.Tooltip, ref v,
                    new FloatRange(floatRow.Min, floatRow.Max, floatRow.Format));
                if (!Mathf.Approximately(v, floatRow.Entry.Value)) floatRow.Entry.Value = v;
                return;
            }

            var keyRow = row as KeyRow;
            if (keyRow == null) return;

            var listenKey = module.Id + ":" + keyRow.Label;
            if (!_listening.ContainsKey(listenKey)) _listening[listenKey] = false;

            var listening = _listening[listenKey];
            var code = keyRow.Entry.Value;
            Widgets.KeyBinder(_theme, keyRow.Label, keyRow.Tooltip, ref code, ref listening);
            _listening[listenKey] = listening;
            if (code != keyRow.Entry.Value) keyRow.Entry.Value = code;
        }

        private void DrawCredits()
        {
            _showCredits = GUILayout.Toggle(_showCredits, _showCredits ? "Credits ▾" : "Credits ▸");
            if (!_showCredits) return;

            GUILayout.Label(
                "Silksong Tweaks is an independent implementation, built against the game's own " +
                "API. It replaces three mods whose ideas it owes a debt to — with thanks to:\n" +
                "  · Xiaohai (XiaohaiMod) — ReBack, for returning to the death location\n" +
                "  · BlueRaja — rosaries never permanently lost, for cocoon merging\n" +
                "  · Ericky1694 — CustomDifficulty, for health and damage tuning\n" +
                "\nMIT licensed. Built on BepInEx and HarmonyX.",
                _theme.Footer);
        }

        private static string[] ArrayOf(IReadOnlyList<string> items)
        {
            var result = new string[items.Count];
            for (var i = 0; i < items.Count; i++) result[i] = items[i];
            return result;
        }
    }
}
