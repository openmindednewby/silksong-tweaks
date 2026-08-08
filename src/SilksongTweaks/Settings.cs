using System;
using BepInEx.Configuration;
using UnityEngine;

namespace SilksongTweaks
{
    /// <summary>A single row the UI can render without knowing which module owns it.</summary>
    public interface ISettingRow
    {
        string Label { get; }
        string Tooltip { get; }
    }

    public sealed class BoolRow : ISettingRow
    {
        public BoolRow(string label, string tooltip, ConfigEntry<bool> entry)
        {
            Label = label;
            Tooltip = tooltip;
            Entry = entry;
        }

        public string Label { get; }
        public string Tooltip { get; }
        public ConfigEntry<bool> Entry { get; }
    }

    public sealed class IntRow : ISettingRow
    {
        public IntRow(string label, string tooltip, ConfigEntry<int> entry, int min, int max)
        {
            Label = label;
            Tooltip = tooltip;
            Entry = entry;
            Min = min;
            Max = max;
        }

        public string Label { get; }
        public string Tooltip { get; }
        public ConfigEntry<int> Entry { get; }
        public int Min { get; }
        public int Max { get; }
    }

    public sealed class FloatRow : ISettingRow
    {
        public FloatRow(string label, string tooltip, ConfigEntry<float> entry, float min, float max, string format)
        {
            Label = label;
            Tooltip = tooltip;
            Entry = entry;
            Min = min;
            Max = max;
            Format = format;
        }

        public string Label { get; }
        public string Tooltip { get; }
        public ConfigEntry<float> Entry { get; }
        public float Min { get; }
        public float Max { get; }

        /// <summary>Numeric format string used for the value readout, e.g. "0.00x".</summary>
        public string Format { get; }
    }

    public sealed class KeyRow : ISettingRow
    {
        public KeyRow(string label, string tooltip, ConfigEntry<KeyCode> entry)
        {
            Label = label;
            Tooltip = tooltip;
            Entry = entry;
        }

        public string Label { get; }
        public string Tooltip { get; }
        public ConfigEntry<KeyCode> Entry { get; }
    }
}
