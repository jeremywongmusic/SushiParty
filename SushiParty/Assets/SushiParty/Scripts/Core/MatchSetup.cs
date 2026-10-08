using UnityEngine;

namespace SushiParty.Core
{
    public enum SessionMode
    {
        SingleMinigame = 0,

        BoardGame = 1,
    }

    public sealed class MatchSetup
    {
        public static readonly Color SlotOneColor = new Color(0.29f, 0.56f, 0.94f);
        public static readonly Color SlotTwoColor = new Color(0.94f, 0.42f, 0.36f);

        public SessionMode Mode { get; set; } = SessionMode.SingleMinigame;

        public MinigameId Minigame { get; set; } = MinigameId.BumperSparks;

        public ControllerKind SlotOneKind { get; set; } = ControllerKind.Human;
        public ControllerKind SlotTwoKind { get; set; } = ControllerKind.Cpu;

        public InputDeviceChoice SlotOneDevice { get; set; } = InputDeviceChoice.KeyboardLeft;
        public InputDeviceChoice SlotTwoDevice { get; set; } = InputDeviceChoice.KeyboardRight;

        public CpuSkill PartnerSkill { get; set; } = CpuSkill.Standard;

        public CpuSkill ChefSkill { get; set; } = CpuSkill.Standard;

        public string SlotOneName => SlotOneKind == ControllerKind.Human ? "Player 1" : "CPU 1";
        public string SlotTwoName => SlotTwoKind == ControllerKind.Human ? "Player 2" : "CPU 2";

        public Participant[] CreateParticipants()
        {
            return new[]
            {
                new Participant(
                    ParticipantSlot.One,
                    SlotOneName,
                    SlotOneColor,
                    SlotOneKind,
                    PartnerSkill,
                    SlotOneDevice),
                new Participant(
                    ParticipantSlot.Two,
                    SlotTwoName,
                    SlotTwoColor,
                    SlotTwoKind,
                    PartnerSkill,
                    SlotTwoDevice),
            };
        }

        public static MatchSetup Debug(MinigameId id)
        {
            return new MatchSetup
            {
                Minigame = id,
                SlotOneKind = ControllerKind.Human,
                SlotTwoKind = ControllerKind.Cpu,
            };
        }
    }
}
