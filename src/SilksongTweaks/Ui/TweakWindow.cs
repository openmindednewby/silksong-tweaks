using System.Collections.Generic;
using SilksongTweaks.Modules;
using UnityEngine;

namespace SilksongTweaks.Ui
{
    /// <summary>
    /// The tweak panel. Renders purely from what modules expose — name, settings, status — so
    /// adding a tweak never touches this file.
    ///
    /// Navigable by mouse, keyboard or gamepad. IMGUI supplies no focus model, so this keeps a
    /// flat index over every editable row and highlights the current one.
    /// </summary>
    public sealed class TweakWindow
    {
        private const int WindowId = 0x51_4B_53;
        private static readonly Vector2 Size = new Vector2(430f, 560f);
        private const int FloatSliderSteps = 20;

        private readonly ModuleRegistry _registry;
        private readonly Theme _theme = new Theme();
        private readonly PanelInput _input = new PanelInput();
        private readonly Dictionary<string, bool> _listening = new Dictionary<string, bool>();
        private readonly List<NavRow> _nav = new List<NavRow>();

        private IReadOnlyList<string> _conflicts;
        private Rect _rect = new Rect(60f, 60f, Size.x, Size.y);
        private Vector2 _scroll;
        private bool _showCredits;
        private int _focus;
        private int _drawIndex;

        public TweakWindow(ModuleRegistry registry) => _registry = registry;

        public bool Visible { get; set; }

        private struct NavRow
        {
            public ITweakModule Module;
            public ISettingRow Row;
        }

        /// <summary>Called from Update so gamepad polling is frame-accurate, not per-GUI-event.</summary>
        public void HandleInput(float unscaledDelta)
        {
            if (!Visible) return;

            RebuildNav();
            if (_nav.Count == 0) return;

            _input.Sample(unscaledDelta);

            if (_input.Cancel)
            {
                Visible = false;
                return;
            }

            if (_input.Vertical != 0)
            {
                _focus = (_focus + _input.Vertical + _nav.Count) % _nav.Count;
            }

            if (_input.Horizontal != 0) Adjust(_nav[_focus], _input.Horizontal);
            if (_input.Activate) Activate(_nav[_focus]);
        }

        private void RebuildNav()
        {
            _nav.Clear();
            foreach (var module in _registry.Modules)
            {
                if (module.Status.State == TweakState.Unavailable) continue;
                foreach (var row in module.Settings)
                    _nav.Add(new NavRow { Module = module, Row = row });
            }

            if (_focus >= _nav.Count) _focus = 0;
        }

        private void Adjust(NavRow nav, int direction)
        {
            if (nav.Row is BoolRow b)
            {
                b.Entry.Value = direction > 0;
                return;
            }

            if (nav.Row is IntRow i)
            {
                i.Entry.Value = Mathf.Clamp(i.Entry.Value + direction, i.Min, i.Max);
                return;
            }

            if (nav.Row is FloatRow f)
            {
                var step = (f.Max - f.Min) / FloatSliderSteps;
                f.Entry.Value = Mathf.Clamp(f.Entry.Value + direction * step, f.Min, f.Max);
            }
        }

        private void Activate(NavRow nav)
        {
            if (nav.Row is BoolRow b)
            {
                b.Entry.Value = !b.Entry.Value;
                return;
            }

            if (nav.Row is KeyRow k)
            {
                var key = nav.Module.Id + ":" + k.Label;
                _listening[key] = !Listening(key);
            }
        }

        private bool Listening(string key) => _listening.TryGetValue(key, out var v) && v;

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
        /// Shown even with the panel closed: during a return countdown you need to know it is
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
            _drawIndex = 0;

            foreach (var module in _registry.Modules) DrawModule(module);

            GUILayout.Space(6f);
            GUILayout.Label(
                "Move: D-pad / left stick / arrows    Change: left-right    Toggle: A / Enter" +
                "\nClose: B / Esc / the open button",
                _theme.Footer);

            GUILayout.Space(6f);
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
                string.Join("\n  · ", ToArray(_conflicts)) +
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

            foreach (var row in module.Settings)
            {
                var selected = _drawIndex == _focus;
                _drawIndex++;
                DrawRow(module, row, selected);
            }

            GUILayout.Space(6f);
        }

        private void DrawRow(ITweakModule module, ISettingRow row, bool selected)
        {
            if (selected) GUILayout.BeginHorizontal(_theme.SelectedRow);

            if (row is BoolRow boolRow)
            {
                var v = boolRow.Entry.Value;
                Widgets.Toggle(_theme, boolRow.Label, boolRow.Tooltip, ref v);
                if (v != boolRow.Entry.Value) boolRow.Entry.Value = v;
            }
            else if (row is IntRow intRow)
            {
                var v = intRow.Entry.Value;
                Widgets.IntSlider(_theme, intRow.Label, intRow.Tooltip, ref v, intRow.Min, intRow.Max);
                if (v != intRow.Entry.Value) intRow.Entry.Value = v;
            }
            else if (row is FloatRow floatRow)
            {
                var v = floatRow.Entry.Value;
                Widgets.FloatSlider(_theme, floatRow.Label, floatRow.Tooltip, ref v,
                    new FloatRange(floatRow.Min, floatRow.Max, floatRow.Format));
                if (!Mathf.Approximately(v, floatRow.Entry.Value)) floatRow.Entry.Value = v;
            }
            else if (row is KeyRow keyRow)
            {
                var key = module.Id + ":" + keyRow.Label;
                var listening = Listening(key);
                var code = keyRow.Entry.Value;
                Widgets.KeyBinder(_theme, keyRow.Label, keyRow.Tooltip, ref code, ref listening);
                _listening[key] = listening;
                if (code != keyRow.Entry.Value) keyRow.Entry.Value = code;
            }

            if (selected) GUILayout.EndHorizontal();
        }

        private void DrawCredits()
        {
            _showCredits = GUILayout.Toggle(_showCredits, _showCredits ? "Credits" : "Credits ...");
            if (!_showCredits) return;

            GUILayout.Label(
                "Silksong Tweaks is an independent implementation, built against the game's own " +
                "API. It replaces three mods whose ideas it owes a debt to — with thanks to:\n" +
                "  - Xiaohai (XiaohaiMod) - ReBack, for returning to the death location\n" +
                "  - BlueRaja - rosaries never permanently lost, for cocoon merging\n" +
                "  - Ericky1694 - CustomDifficulty, for health and damage tuning\n" +
                "\nMIT licensed. Built on BepInEx and HarmonyX.",
                _theme.Footer);
        }

        private static string[] ToArray(IReadOnlyList<string> items)
        {
            var result = new string[items.Count];
            for (var i = 0; i < items.Count; i++) result[i] = items[i];
            return result;
        }
    }
}
