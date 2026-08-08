using UnityEngine;

namespace SilksongTweaks.Ui
{
    /// <summary>
    /// All styling in one place, so visual iteration never risks touching behaviour and every
    /// widget stays consistent by construction rather than by discipline.
    ///
    /// GUIStyle objects can only be created while a GUI is being drawn, so everything here is
    /// built lazily on first draw rather than in a constructor.
    /// </summary>
    public sealed class Theme
    {
        public static readonly Color Silk = new Color(0.93f, 0.91f, 0.86f);
        public static readonly Color Muted = new Color(0.62f, 0.60f, 0.58f);
        public static readonly Color Accent = new Color(0.85f, 0.44f, 0.52f);
        public static readonly Color Good = new Color(0.55f, 0.80f, 0.55f);
        public static readonly Color Warn = new Color(0.92f, 0.74f, 0.35f);
        public static readonly Color Bad = new Color(0.88f, 0.45f, 0.45f);
        public static readonly Color Panel = new Color(0.09f, 0.09f, 0.12f, 0.96f);

        private bool _built;

        public GUIStyle Window { get; private set; }
        public GUIStyle SectionTitle { get; private set; }
        public GUIStyle SectionDesc { get; private set; }
        public GUIStyle RowLabel { get; private set; }
        public GUIStyle Value { get; private set; }
        public GUIStyle Badge { get; private set; }
        public GUIStyle Footer { get; private set; }
        public GUIStyle Toast { get; private set; }

        public void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            Window = new GUIStyle(GUI.skin.window)
            {
                padding = new RectOffset(14, 14, 24, 12),
            };
            Window.normal.background = SolidTexture(Panel);
            Window.onNormal.background = Window.normal.background;
            Window.normal.textColor = Silk;
            Window.onNormal.textColor = Silk;
            Window.fontStyle = FontStyle.Bold;

            SectionTitle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14,
                margin = new RectOffset(0, 0, 10, 2),
            };
            SectionTitle.normal.textColor = Accent;

            SectionDesc = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            SectionDesc.normal.textColor = Muted;

            RowLabel = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            RowLabel.normal.textColor = Silk;

            Value = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold,
            };
            Value.normal.textColor = Silk;

            Badge = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold,
            };

            Footer = new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true };
            Footer.normal.textColor = Muted;

            Toast = new GUIStyle(GUI.skin.box)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(18, 18, 12, 12),
            };
            Toast.normal.background = SolidTexture(new Color(0.06f, 0.06f, 0.08f, 0.88f));
            Toast.normal.textColor = Silk;
        }

        private static Texture2D SolidTexture(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }
    }
}
