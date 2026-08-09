using UnityEngine;

namespace SilksongTweaks.Ui
{
    /// <summary>
    /// Directional and button input for the panel, from either a gamepad or the keyboard.
    ///
    /// IMGUI has no navigation model of its own, so this supplies one: a debounced "step"
    /// signal that fires once on press and then repeats while held, which is what makes a
    /// stick usable for moving between rows without flying past them.
    /// </summary>
    public sealed class PanelInput
    {
        private const float DeadZone = 0.5f;
        private const float FirstRepeatDelay = 0.35f;
        private const float RepeatInterval = 0.09f;

        private static bool _axesUnavailable;

        private float _verticalHeldFor;
        private float _horizontalHeldFor;
        private int _lastVertical;
        private int _lastHorizontal;

        /// <summary>-1 up, +1 down, 0 none. Repeats while held.</summary>
        public int Vertical { get; private set; }

        /// <summary>-1 left, +1 right, 0 none. Repeats while held.</summary>
        public int Horizontal { get; private set; }

        public bool Activate { get; private set; }
        public bool Cancel { get; private set; }

        public void Sample(float unscaledDelta)
        {
            Vertical = Step(RawVertical(), ref _lastVertical, ref _verticalHeldFor, unscaledDelta);
            Horizontal = Step(RawHorizontal(), ref _lastHorizontal, ref _horizontalHeldFor, unscaledDelta);

            Activate = Input.GetKeyDown(KeyCode.JoystickButton0)
                       || Input.GetKeyDown(KeyCode.Return)
                       || Input.GetKeyDown(KeyCode.Space);

            Cancel = Input.GetKeyDown(KeyCode.JoystickButton1)
                     || Input.GetKeyDown(KeyCode.Escape);
        }

        private static int RawVertical()
        {
            if (Input.GetKey(KeyCode.UpArrow)) return -1;
            if (Input.GetKey(KeyCode.DownArrow)) return 1;

            var axis = SafeAxis("Vertical");
            if (axis > DeadZone) return -1;
            if (axis < -DeadZone) return 1;
            return 0;
        }

        private static int RawHorizontal()
        {
            if (Input.GetKey(KeyCode.LeftArrow)) return -1;
            if (Input.GetKey(KeyCode.RightArrow)) return 1;

            var axis = SafeAxis("Horizontal");
            if (axis < -DeadZone) return -1;
            if (axis > DeadZone) return 1;
            return 0;
        }

        /// <summary>
        /// Axis names are defined by the GAME's input manager, not by us. Reading one that does
        /// not exist throws, and doing that every frame would cost more than the feature is
        /// worth, so the first failure disables axis reading and leaves the keyboard working.
        /// </summary>
        private static float SafeAxis(string name)
        {
            if (_axesUnavailable) return 0f;

            try
            {
                return Input.GetAxisRaw(name);
            }
            catch
            {
                _axesUnavailable = true;
                Plugin.Log.LogWarning(
                    $"Input axis '{name}' is not defined by the game; gamepad sticks disabled. " +
                    "Keyboard navigation still works.");
                return 0f;
            }
        }

        private static int Step(int raw, ref int last, ref float heldFor, float delta)
        {
            if (raw == 0)
            {
                last = 0;
                heldFor = 0f;
                return 0;
            }

            if (raw != last)
            {
                last = raw;
                heldFor = 0f;
                return raw;
            }

            heldFor += delta;
            if (heldFor < FirstRepeatDelay) return 0;

            heldFor -= RepeatInterval;
            return raw;
        }
    }
}
