using System.Collections.Generic;
using SushiParty.Core;
using UnityEngine;

namespace SushiParty.Menu
{
    public enum SkillDial
    {
        Partner = 0,

        Chef = 1,
    }

    public sealed class MenuSelection
    {
        public const int DefaultColumns = 3;
        private readonly IReadOnlyList<MinigameDefinition> entries;
        private readonly MatchSetup setup;
        private readonly int columns;
        private int selectedIndex;

        public MenuSelection(IReadOnlyList<MinigameDefinition> entries, MatchSetup setup, int columns = DefaultColumns)
        {
            this.entries = entries;
            this.setup = setup;
            this.columns = Mathf.Max(1, columns);

            selectedIndex = IndexOf(setup.Minigame);
        }

        public IReadOnlyList<MinigameDefinition> Entries => entries;
        public int Columns => columns;
        public int SelectedIndex => selectedIndex;

        public MinigameDefinition Selected =>
            selectedIndex >= 0 && selectedIndex < entries.Count ? entries[selectedIndex] : null;

        public SessionMode Mode => setup.Mode;
        public CpuSkill PartnerSkill => setup.PartnerSkill;
        public CpuSkill ChefSkill => setup.ChefSkill;

        public ControllerKind SeatKind(ParticipantSlot slot)
        {
            return slot == ParticipantSlot.One ? setup.SlotOneKind : setup.SlotTwoKind;
        }

        public InputDeviceChoice SeatDevice(ParticipantSlot slot)
        {
            return slot == ParticipantSlot.One ? setup.SlotOneDevice : setup.SlotTwoDevice;
        }

        public bool Navigate(Vector2Int direction)
        {
            int count = entries.Count;
            if (count == 0)
            {
                return false;
            }

            int column = selectedIndex % columns;
            int row = selectedIndex / columns;
            int rows = Mathf.CeilToInt(count / (float)columns);

            if (direction.x != 0)
            {
                column = Wrap(column + (direction.x > 0 ? 1 : -1), columns);
            }

            if (direction.y != 0)
            {
                row = Wrap(row - (direction.y > 0 ? 1 : -1), rows);
            }

            int previousIndex = selectedIndex;
            selectedIndex = Mathf.Clamp(row * columns + column, 0, count - 1);

            return selectedIndex != previousIndex;
        }

        public bool SelectIndex(int index)
        {
            if (index < 0 || index >= entries.Count || index == selectedIndex)
            {
                return false;
            }

            selectedIndex = index;
            return true;
        }

        public bool CanLaunch(int index)
        {
            return index >= 0 && index < entries.Count;
        }

        public MinigameDefinition Confirm()
        {
            if (!CanLaunch(selectedIndex))
            {
                return null;
            }

            MinigameDefinition entry = entries[selectedIndex];
            setup.Minigame = entry.Id;
            setup.Mode = SessionMode.SingleMinigame;

            return entry;
        }

        public void ConfirmBoardGame()
        {
            setup.Mode = SessionMode.BoardGame;
        }

        public void ToggleSeat(ParticipantSlot slot)
        {
            if (slot == ParticipantSlot.One)
            {
                setup.SlotOneKind = Flip(setup.SlotOneKind);
                return;
            }

            setup.SlotTwoKind = Flip(setup.SlotTwoKind);
        }

        public void CycleSkill(SkillDial dial)
        {
            if (dial == SkillDial.Partner)
            {
                setup.PartnerSkill = Cycle(setup.PartnerSkill);
                return;
            }

            setup.ChefSkill = Cycle(setup.ChefSkill);
        }

        public static string Describe(ControllerKind kind)
        {
            return kind == ControllerKind.Human ? "Human" : "CPU";
        }

        private static ControllerKind Flip(ControllerKind kind)
        {
            return kind == ControllerKind.Human ? ControllerKind.Cpu : ControllerKind.Human;
        }

        private static CpuSkill Cycle(CpuSkill skill)
        {
            return skill switch
            {
                CpuSkill.Relaxed => CpuSkill.Standard,
                CpuSkill.Standard => CpuSkill.Sharp,
                _ => CpuSkill.Relaxed,
            };
        }

        private static int Wrap(int value, int span)
        {
            if (span <= 0)
            {
                return 0;
            }

            return ((value % span) + span) % span;
        }

        private int IndexOf(MinigameId id)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Id == id)
                {
                    return i;
                }
            }

            return 0;
        }
    }
}
