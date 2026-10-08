using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SushiParty.Menu
{
    public enum TitleChoice
    {
        PlayBoard = 0,

        PlayMinigames = 1,

        Settings = 2,
    }

    public sealed class TitleSelection
    {
        private static readonly IReadOnlyList<TitleChoice> All =
            new ReadOnlyCollection<TitleChoice>(new List<TitleChoice>
            {
                TitleChoice.PlayBoard,
                TitleChoice.PlayMinigames,
                TitleChoice.Settings,
            });

        private int selectedIndex;
        public IReadOnlyList<TitleChoice> Entries => All;
        public int SelectedIndex => selectedIndex;
        public TitleChoice Selected => All[selectedIndex];

        public bool Navigate(int direction)
        {
            if (direction == 0)
            {
                return false;
            }

            int step = direction > 0 ? 1 : -1;
            int next = ((selectedIndex + step) % All.Count + All.Count) % All.Count;

            if (next == selectedIndex)
            {
                return false;
            }

            selectedIndex = next;
            return true;
        }

        public bool SelectIndex(int index)
        {
            if (index < 0 || index >= All.Count || index == selectedIndex)
            {
                return false;
            }

            selectedIndex = index;
            return true;
        }

        public static string Caption(TitleChoice choice)
        {
            switch (choice)
            {
                case TitleChoice.PlayBoard:
                    return "PLAY BOARD";
                case TitleChoice.PlayMinigames:
                    return "PLAY MINIGAMES";
                default:
                    return "SETTINGS";
            }
        }

        public static string Blurb(TitleChoice choice)
        {
            switch (choice)
            {
                case TitleChoice.PlayBoard:
                    return "Ten rounds on the ring. A minigame at the end of every one.";
                case TitleChoice.PlayMinigames:
                    return "Pick any of the ten and play it now, on its own.";
                default:
                    return "Volumes, mutes, and what the mixer is doing.";
            }
        }
    }
}
