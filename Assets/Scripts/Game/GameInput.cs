using ShapeShifter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShapeShifter.Game
{
    /// <summary>
    /// Keyboard and gamepad through the Input System (touch and mouse go through the HUD's
    /// buttons). Play: 1-4 or K / I / L / J pick fox, bird, golem, fish (the same diamond as the
    /// pad's A / Y / B / X); Space, W or Up jumps (pad: a shoulder, a trigger, d-pad or stick
    /// up); Esc, P or Start pauses. Menus: arrows or d-pad, Enter / Space / A to choose, Esc / B back.
    /// </summary>
    public sealed class GameInput
    {
        int lastStickY;
        int lastStickX;
        bool stickJumpHeld;

        static Keyboard Keys => Keyboard.current;
        static Gamepad Pad => Gamepad.current;

        /// <summary>True when the gamepad was used last (the HUD shows pad prompts then).</summary>
        public bool UsingGamepad { get; private set; }

        /// <summary>The form asked for this frame, if any.</summary>
        public Form? ShiftPressed()
        {
            Form? form = null;
            Keyboard k = Keys;
            if (k != null)
            {
                if (k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame || k.kKey.wasPressedThisFrame) form = Form.Fox;
                if (k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame || k.iKey.wasPressedThisFrame) form = Form.Bird;
                if (k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame || k.lKey.wasPressedThisFrame) form = Form.Golem;
                if (k.digit4Key.wasPressedThisFrame || k.numpad4Key.wasPressedThisFrame || k.jKey.wasPressedThisFrame) form = Form.Fish;
                if (form.HasValue) UsingGamepad = false;
            }
            Gamepad p = Pad;
            if (p != null)
            {
                Form? pad = null;
                if (p.buttonSouth.wasPressedThisFrame) pad = Form.Fox;
                if (p.buttonNorth.wasPressedThisFrame) pad = Form.Bird;
                if (p.buttonEast.wasPressedThisFrame) pad = Form.Golem;
                if (p.buttonWest.wasPressedThisFrame) pad = Form.Fish;
                if (pad.HasValue)
                {
                    form = pad;
                    UsingGamepad = true;
                }
            }
            return form;
        }

        public bool JumpPressed()
        {
            bool jump = false;
            Keyboard k = Keys;
            if (k != null && (k.spaceKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame))
            {
                jump = true;
                UsingGamepad = false;
            }
            Gamepad p = Pad;
            if (p != null)
            {
                bool stickUp = p.leftStick.ReadValue().y > 0.6f;
                bool pressed = p.leftShoulder.wasPressedThisFrame || p.rightShoulder.wasPressedThisFrame ||
                               p.leftTrigger.wasPressedThisFrame || p.rightTrigger.wasPressedThisFrame ||
                               p.dpad.up.wasPressedThisFrame || (stickUp && !stickJumpHeld);
                stickJumpHeld = stickUp;
                if (pressed)
                {
                    jump = true;
                    UsingGamepad = true;
                }
            }
            return jump;
        }

        public bool Pause =>
            (Keys != null && (Keys.escapeKey.wasPressedThisFrame || Keys.pKey.wasPressedThisFrame)) ||
            (Pad != null && Pad.startButton.wasPressedThisFrame);

        public bool Confirm =>
            (Keys != null && (Keys.enterKey.wasPressedThisFrame || Keys.numpadEnterKey.wasPressedThisFrame || Keys.spaceKey.wasPressedThisFrame)) ||
            (Pad != null && (Pad.buttonSouth.wasPressedThisFrame || Pad.startButton.wasPressedThisFrame));

        public bool Back =>
            (Keys != null && (Keys.escapeKey.wasPressedThisFrame || Keys.backspaceKey.wasPressedThisFrame)) ||
            (Pad != null && Pad.buttonEast.wasPressedThisFrame);

        /// <summary>-1 for up, +1 for down, once per press.</summary>
        public int Vertical()
        {
            int step = 0;
            if (Keys != null)
            {
                if (Keys.upArrowKey.wasPressedThisFrame || Keys.wKey.wasPressedThisFrame) step = -1;
                if (Keys.downArrowKey.wasPressedThisFrame || Keys.sKey.wasPressedThisFrame) step = 1;
            }
            if (Pad != null)
            {
                if (Pad.dpad.up.wasPressedThisFrame) step = -1;
                if (Pad.dpad.down.wasPressedThisFrame) step = 1;
                float y = Pad.leftStick.ReadValue().y;
                int sign = y > 0.6f ? -1 : y < -0.6f ? 1 : 0;
                if (sign != 0 && sign != lastStickY) step = sign;
                lastStickY = sign;
            }
            return step;
        }

        /// <summary>-1 for left, +1 for right, once per press.</summary>
        public int Horizontal()
        {
            int step = 0;
            if (Keys != null)
            {
                if (Keys.leftArrowKey.wasPressedThisFrame || Keys.aKey.wasPressedThisFrame) step = -1;
                if (Keys.rightArrowKey.wasPressedThisFrame || Keys.dKey.wasPressedThisFrame) step = 1;
            }
            if (Pad != null)
            {
                if (Pad.dpad.left.wasPressedThisFrame) step = -1;
                if (Pad.dpad.right.wasPressedThisFrame) step = 1;
                float x = Pad.leftStick.ReadValue().x;
                int sign = x > 0.6f ? 1 : x < -0.6f ? -1 : 0;
                if (sign != 0 && sign != lastStickX) step = sign;
                lastStickX = sign;
            }
            return step;
        }

        public static bool HasTouchScreen => Touchscreen.current != null;
    }
}
