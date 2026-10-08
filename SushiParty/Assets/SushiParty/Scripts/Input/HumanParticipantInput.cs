using SushiParty.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SushiParty.InputLayer
{
    public readonly struct KeyboardScheme
    {
        public readonly Key[] Up;
        public readonly Key[] Down;
        public readonly Key[] Left;
        public readonly Key[] Right;
        public readonly Key[] Primary;
        public readonly Key[] Secondary;
        public readonly string PrimaryLabel;
        public readonly string SecondaryLabel;
        public readonly string MoveLabel;

        public KeyboardScheme(
            Key[] up, Key[] down, Key[] left, Key[] right,
            Key[] primary, Key[] secondary,
            string moveLabel, string primaryLabel, string secondaryLabel)
        {
            Up = up;
            Down = down;
            Left = left;
            Right = right;
            Primary = primary;
            Secondary = secondary;
            MoveLabel = moveLabel;
            PrimaryLabel = primaryLabel;
            SecondaryLabel = secondaryLabel;
        }

        public static readonly KeyboardScheme LeftSide = new KeyboardScheme(
            up: new[] { Key.W },
            down: new[] { Key.S },
            left: new[] { Key.A },
            right: new[] { Key.D },
            primary: new[] { Key.Space, Key.F },
            secondary: new[] { Key.LeftShift, Key.G },
            moveLabel: "WASD",
            primaryLabel: "Space",
            secondaryLabel: "Left Shift");

        public static readonly KeyboardScheme RightSide = new KeyboardScheme(
            up: new[] { Key.UpArrow },
            down: new[] { Key.DownArrow },
            left: new[] { Key.LeftArrow },
            right: new[] { Key.RightArrow },
            primary: new[] { Key.RightCtrl, Key.Numpad0, Key.Period },
            secondary: new[] { Key.RightShift, Key.Numpad1, Key.Slash },
            moveLabel: "Arrow Keys",
            primaryLabel: "Right Ctrl / Numpad 0",
            secondaryLabel: "Right Shift / Numpad 1");

        public static KeyboardScheme For(InputDeviceChoice device)
        {
            return device == InputDeviceChoice.KeyboardRight ? RightSide : LeftSide;
        }
    }

    public sealed class HumanParticipantInput : IParticipantInput
    {
        private readonly InputDeviceChoice device;
        private readonly KeyboardScheme scheme;
        private readonly bool usesGamepad;
        private readonly int gamepadIndex;

        public HumanParticipantInput(InputDeviceChoice device)
        {
            this.device = device;
            scheme = KeyboardScheme.For(device);
            usesGamepad = device == InputDeviceChoice.Gamepad1 || device == InputDeviceChoice.Gamepad2;
            gamepadIndex = device == InputDeviceChoice.Gamepad2 ? 1 : 0;
        }

        public InputDeviceChoice Device => device;

        public Vector2 Move
        {
            get
            {
                if (usesGamepad)
                {
                    Gamepad pad = ResolveGamepad();
                    if (pad == null)
                    {
                        return Vector2.zero;
                    }

                    Vector2 stick = pad.leftStick.ReadValue() + pad.dpad.ReadValue();
                    return Vector2.ClampMagnitude(stick, 1f);
                }

                Keyboard keyboard = Keyboard.current;
                if (keyboard == null)
                {
                    return Vector2.zero;
                }

                float x = (AnyHeld(keyboard, scheme.Right) ? 1f : 0f) - (AnyHeld(keyboard, scheme.Left) ? 1f : 0f);
                float y = (AnyHeld(keyboard, scheme.Up) ? 1f : 0f) - (AnyHeld(keyboard, scheme.Down) ? 1f : 0f);
                return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            }
        }

        public Vector2 Aim
        {
            get
            {
                if (usesGamepad)
                {
                    Gamepad pad = ResolveGamepad();
                    return pad == null ? Vector2.zero : pad.rightStick.ReadValue();
                }

                Mouse mouse = Mouse.current;
                return mouse == null ? Vector2.zero : mouse.position.ReadValue();
            }
        }

        public bool IsHeld(MinigameAction action)
        {
            if (usesGamepad)
            {
                Gamepad pad = ResolveGamepad();
                if (pad == null)
                {
                    return false;
                }

                return action == MinigameAction.Primary
                    ? pad.buttonSouth.isPressed
                    : pad.buttonWest.isPressed || pad.buttonEast.isPressed;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            return AnyHeld(keyboard, action == MinigameAction.Primary ? scheme.Primary : scheme.Secondary);
        }

        public bool WasPressed(MinigameAction action)
        {
            if (usesGamepad)
            {
                Gamepad pad = ResolveGamepad();
                if (pad == null)
                {
                    return false;
                }

                return action == MinigameAction.Primary
                    ? pad.buttonSouth.wasPressedThisFrame
                    : pad.buttonWest.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            return AnyPressed(keyboard, action == MinigameAction.Primary ? scheme.Primary : scheme.Secondary);
        }

        public void Tick(float deltaTime)
        {
        }

        public string DescribeControls()
        {
            if (usesGamepad)
            {
                return $"Gamepad {gamepadIndex + 1}  ·  Stick to move  ·  A  ·  X";
            }

            return $"{scheme.MoveLabel}  ·  {scheme.PrimaryLabel}  ·  {scheme.SecondaryLabel}";
        }

        private Gamepad ResolveGamepad()
        {
            var pads = Gamepad.all;
            return gamepadIndex < pads.Count ? pads[gamepadIndex] : null;
        }

        private static bool AnyHeld(Keyboard keyboard, Key[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (keyboard[keys[i]].isPressed)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AnyPressed(Keyboard keyboard, Key[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (keyboard[keys[i]].wasPressedThisFrame)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
