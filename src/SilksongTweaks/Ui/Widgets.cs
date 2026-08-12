using System;
using UnityEngine;

namespace SilksongTweaks.Ui
{
    /// <summary>Styled primitives. Each renders one setting row and writes straight to config.</summary>
    public static class Widgets
    {
        private const float LabelWidth = 240f;
        private const float ValueWidth = 76f;
        private const float RowHeight = 22f;

        public static void Toggle(Theme theme, string label, string tooltip, ref bool value)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
            GUILayout.Label(new GUIContent(label, tooltip), theme.RowLabel, GUILayout.Width(LabelWidth));
            GUILayout.FlexibleSpace();
            value = GUILayout.Toggle(value, value ? " ON" : " OFF", GUILayout.Width(ValueWidth));
            GUILayout.EndHorizontal();
        }

        public static void IntSlider(
            Theme theme, string label, string tooltip, ref int value, int min, int max)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
            GUILayout.Label(new GUIContent(label, tooltip), theme.RowLabel, GUILayout.Width(LabelWidth));
            value = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max));
            GUILayout.Label(value.ToString(), theme.Value, GUILayout.Width(ValueWidth));
            GUILayout.EndHorizontal();
        }

        public static void FloatSlider(
            Theme theme, string label, string tooltip, ref float value, FloatRange range)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
            GUILayout.Label(new GUIContent(label, tooltip), theme.RowLabel, GUILayout.Width(LabelWidth));
            value = GUILayout.HorizontalSlider(value, range.Min, range.Max);
            GUILayout.Label(Format(value, range.Format), theme.Value, GUILayout.Width(ValueWidth));
            GUILayout.EndHorizontal();
        }

        /// <summary>Returns true on the frame the button is pressed.</summary>
        public static bool Button(Theme theme, string label, string tooltip, string caption)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
            GUILayout.Label(new GUIContent(label, tooltip), theme.RowLabel, GUILayout.Width(LabelWidth));
            GUILayout.FlexibleSpace();
            var pressed = GUILayout.Button(caption, GUILayout.Width(110f));
            GUILayout.EndHorizontal();
            return pressed;
        }

        public static void KeyBinder(
            Theme theme, string label, string tooltip, ref KeyCode value, ref bool listening)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
            GUILayout.Label(new GUIContent(label, tooltip), theme.RowLabel, GUILayout.Width(LabelWidth));
            GUILayout.FlexibleSpace();

            var caption = listening ? "press a key..." : value.ToString();
            if (GUILayout.Button(caption, GUILayout.Width(110f)))
            {
                listening = !listening;
            }

            if (listening && Event.current != null && Event.current.isKey && Event.current.keyCode != KeyCode.None)
            {
                value = Event.current.keyCode;
                listening = false;
                Event.current.Use();
            }

            GUILayout.EndHorizontal();
        }

        public static void StatusBadge(Theme theme, TweakStatus status, long lastFiredTicks)
        {
            string text;
            Color color;

            switch (status.State)
            {
                case TweakState.Active:
                    text = lastFiredTicks > 0 ? "ACTIVE  ·  fired " + Ago(lastFiredTicks) : "ACTIVE  ·  not yet fired";
                    color = lastFiredTicks > 0 ? Theme.Good : Theme.Warn;
                    break;
                case TweakState.Disabled:
                    text = "OFF";
                    color = Theme.Muted;
                    break;
                default:
                    text = "UNAVAILABLE  ·  " + status.Reason;
                    color = Theme.Bad;
                    break;
            }

            var previous = theme.Badge.normal.textColor;
            theme.Badge.normal.textColor = color;
            GUILayout.Label(text, theme.Badge);
            theme.Badge.normal.textColor = previous;
        }

        private static string Format(float value, string format)
        {
            if (format == "0%") return Mathf.RoundToInt(value * 100f) + "%";
            if (format == "0.0s") return value.ToString("0.0") + "s";
            return value.ToString("0.00") + "x";
        }

        private static string Ago(long utcTicks)
        {
            var seconds = (DateTime.UtcNow - new DateTime(utcTicks, DateTimeKind.Utc)).TotalSeconds;
            if (seconds < 2) return "just now";
            if (seconds < 60) return (int)seconds + "s ago";
            if (seconds < 3600) return (int)(seconds / 60) + "m ago";
            return (int)(seconds / 3600) + "h ago";
        }
    }

    /// <summary>Bundles slider bounds so widget calls stay within the four-parameter limit.</summary>
    public struct FloatRange
    {
        public FloatRange(float min, float max, string format)
        {
            Min = min;
            Max = max;
            Format = format;
        }

        public float Min { get; }
        public float Max { get; }
        public string Format { get; }
    }
}
