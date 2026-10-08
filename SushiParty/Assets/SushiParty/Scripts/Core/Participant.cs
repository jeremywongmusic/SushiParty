using SushiParty.InputLayer;
using UnityEngine;

namespace SushiParty.Core
{
    public enum ParticipantSlot
    {
        One = 0,
        Two = 1,
    }

    public enum ControllerKind
    {
        Human = 0,
        Cpu = 1,
    }

    public enum CpuSkill
    {
        Relaxed = 0,
        Standard = 1,
        Sharp = 2,
    }

    public enum InputDeviceChoice
    {
        KeyboardLeft = 0,
        KeyboardRight = 1,
        Gamepad1 = 2,
        Gamepad2 = 3,
    }

    public sealed class Participant
    {
        public ParticipantSlot Slot { get; }
        public string DisplayName { get; }
        public Color Color { get; }
        public ControllerKind Kind { get; }
        public CpuSkill Skill { get; }
        public InputDeviceChoice Device { get; }

        public IParticipantInput Input { get; internal set; }

        public bool IsCpu => Kind == ControllerKind.Cpu;

        public Participant(
            ParticipantSlot slot,
            string displayName,
            Color color,
            ControllerKind kind,
            CpuSkill skill,
            InputDeviceChoice device)
        {
            Slot = slot;
            DisplayName = displayName;
            Color = color;
            Kind = kind;
            Skill = skill;
            Device = device;
            Input = kind == ControllerKind.Human
                ? new HumanParticipantInput(device)
                : NullParticipantInput.Instance;
        }
    }
}
