using NUnit.Framework;
using SushiParty.Menu;

namespace SushiParty.Tests
{
    public sealed class TitleSelectionTests
    {
        [Test]
        public void It_offers_the_board_first()
        {
            TitleSelection title = new TitleSelection();

            Assert.That(title.Entries.Count, Is.EqualTo(3));
            Assert.That(title.Selected, Is.EqualTo(TitleChoice.PlayBoard),
                "the board is the main event, so it is what the cursor opens on");
        }

        [Test]
        public void The_three_entries_are_the_three_ways_in()
        {
            TitleSelection title = new TitleSelection();

            Assert.That(title.Entries, Is.EquivalentTo(new[]
            {
                TitleChoice.PlayBoard,
                TitleChoice.PlayMinigames,
                TitleChoice.Settings,
            }));
        }

        [Test]
        public void The_cursor_walks_down_the_list()
        {
            TitleSelection title = new TitleSelection();

            Assert.That(title.Navigate(1), Is.True);
            Assert.That(title.Selected, Is.EqualTo(TitleChoice.PlayMinigames));

            Assert.That(title.Navigate(1), Is.True);
            Assert.That(title.Selected, Is.EqualTo(TitleChoice.Settings));
        }

        [Test]
        public void It_wraps_off_both_ends()
        {
            TitleSelection title = new TitleSelection();

            Assert.That(title.Navigate(-1), Is.True);
            Assert.That(title.Selected, Is.EqualTo(TitleChoice.Settings),
                "up off the top comes back at the bottom");

            Assert.That(title.Navigate(1), Is.True);
            Assert.That(title.Selected, Is.EqualTo(TitleChoice.PlayBoard));
        }

        [Test]
        public void A_nudge_of_nothing_moves_nothing()
        {
            TitleSelection title = new TitleSelection();

            Assert.That(title.Navigate(0), Is.False);
            Assert.That(title.SelectedIndex, Is.EqualTo(0));
        }

        [Test]
        public void Pointing_at_nothing_leaves_the_cursor_alone()
        {
            TitleSelection title = new TitleSelection();
            title.SelectIndex(1);

            Assert.That(title.SelectIndex(-1), Is.False);
            Assert.That(title.SelectIndex(3), Is.False);
            Assert.That(title.SelectIndex(1), Is.False, "already resting on it");
            Assert.That(title.SelectedIndex, Is.EqualTo(1));
        }

        [Test]
        public void Every_entry_has_something_to_say_for_itself()
        {
            foreach (TitleChoice choice in System.Enum.GetValues(typeof(TitleChoice)))
            {
                Assert.That(TitleSelection.Caption(choice), Is.Not.Null.And.Not.Empty, choice.ToString());
                Assert.That(TitleSelection.Blurb(choice), Is.Not.Null.And.Not.Empty, choice.ToString());
            }
        }

        [Test]
        public void The_two_play_options_explain_which_is_which()
        {
            Assert.That(TitleSelection.Blurb(TitleChoice.PlayBoard),
                Is.Not.EqualTo(TitleSelection.Blurb(TitleChoice.PlayMinigames)));
            Assert.That(TitleSelection.Caption(TitleChoice.PlayBoard),
                Is.Not.EqualTo(TitleSelection.Caption(TitleChoice.PlayMinigames)));
        }
    }
}
