using NUnit.Framework;
using SushiParty.Audio;
using SushiParty.Board;
using SushiParty.Core;
using UnityEngine;

namespace SushiParty.Tests
{
    public sealed class BoardAudioReporterTests
    {
        private RecordedGlobals published;
        private BoardAudioReporter reporter;

        [SetUp]
        public void SetUp()
        {
            published = new RecordedGlobals();
            reporter = new BoardAudioReporter(published);
        }

        private static BoardLayout PayoutRing(params (int index, SpaceKind kind)[] overrides)
        {
            const int count = 24;
            SpaceKind[] kinds = new SpaceKind[count];
            Vector2[] positions = new Vector2[count];
            int[][] exits = new int[count][];

            for (int i = 0; i < count; i++)
            {
                kinds[i] = SpaceKind.CoinGain;
                positions[i] = new Vector2(i, 0f);
                exits[i] = new[] { (i + 1) % count };
            }

            kinds[0] = SpaceKind.Start;
            foreach ((int index, SpaceKind kind) in overrides)
            {
                kinds[index] = kind;
            }

            return new BoardLayout(kinds, positions, exits);
        }

        private static BoardSession Session(BoardLayout layout = null, int startingCoins = 5)
        {
            return new BoardSession(layout ?? PayoutRing(), new ScriptedDie(1), startingCoins: startingCoins);
        }

        [Test]
        public void The_pair_coin_total_is_published()
        {
            BoardSession session = Session(startingCoins: 5);

            reporter.Publish(session, null);

            Assert.That(published.Number(Global.Coins), Is.EqualTo(10f), "five each, across the two of them");
        }

        [Test]
        public void Chef_Takos_pile_is_not_part_of_the_pairs_total()
        {
            BoardSession session = Session(startingCoins: 5);

            Assert.That(BoardAudioReporter.CoinsHeldByThePair(session), Is.EqualTo(10),
                "the stakes are the pair's, so the third token's coins are not their number");
        }

        [Test]
        public void The_first_look_at_a_session_reports_no_swing()
        {
            reporter.Publish(Session(startingCoins: 9), null);

            Assert.That(published.Number(Global.CoinSwing), Is.EqualTo(0f),
                "opening coins are not a windfall that just happened");
        }

        [Test]
        public void A_payout_shows_up_as_a_positive_swing_once()
        {
            BoardSession session = Session();
            reporter.Publish(session, null);

            session.TakeTurn();
            reporter.Publish(session, null);

            Assert.That(published.Number(Global.CoinSwing), Is.GreaterThan(0f), "a plate paid out");

            reporter.Publish(session, null);
            Assert.That(published.Number(Global.CoinSwing), Is.EqualTo(0f),
                "a swing is a spike, not a level — it would never come back down otherwise");
        }

        [Test]
        public void Chef_Tako_taking_a_cut_shows_up_as_a_negative_swing()
        {
            BoardSession session = Session(PayoutRing((1, SpaceKind.CoinLoss)));
            reporter.Publish(session, null);

            session.TakeTurn();
            reporter.Publish(session, null);

            Assert.That(published.Number(Global.CoinSwing), Is.LessThan(0f),
                "the sign is what tells a loss from a gain");
        }

        [Test]
        public void The_pairs_total_tracks_what_the_session_says_it_is()
        {
            BoardSession session = Session();

            session.TakeTurn();
            reporter.Publish(session, null);

            Assert.That(published.Number(Global.Coins),
                Is.EqualTo((float)BoardAudioReporter.CoinsHeldByThePair(session)));
        }

        [Test]
        public void Resetting_stops_a_new_session_inheriting_the_old_ones_total()
        {
            BoardSession first = Session(startingCoins: 20);
            reporter.Publish(first, null);

            reporter.Reset();

            BoardSession second = Session(startingCoins: 0);
            reporter.Publish(second, null);

            Assert.That(published.Number(Global.CoinSwing), Is.EqualTo(0f),
                "sitting down at a fresh board is not a loss of forty coins");
        }

        [Test]
        public void The_round_number_is_published_as_it_reads_on_the_HUD()
        {
            BoardSession session = Session();

            reporter.Publish(session, null);

            Assert.That(published.Number(Global.BoardRound), Is.EqualTo((float)session.Round));
        }

        [Test]
        public void Progress_starts_at_zero_and_survives_no_session_at_all()
        {
            Assert.That(BoardAudioReporter.Progress(Session()), Is.EqualTo(0f).Within(0.0001f),
                "round one has none of the session behind it");
            Assert.That(BoardAudioReporter.Progress(null), Is.EqualTo(0f), "and no session is not a crash");
        }

        [Test]
        public void Progress_is_published_between_zero_and_one()
        {
            BoardSession session = Session();

            reporter.Publish(session, null);

            Assert.That(published.Number(Global.BoardProgress), Is.InRange(0f, 1f));
        }

        [Test]
        public void Chef_Takos_skill_is_published_as_a_label()
        {
            MatchSetup setup = new MatchSetup { ChefSkill = CpuSkill.Sharp };

            reporter.Publish(Session(), setup);

            Assert.That(published.Label(Global.Difficulty), Is.EqualTo("Sharp"),
                "labels come off the enum so Studio and C# cannot drift apart");
        }

        [Test]
        public void The_number_of_people_playing_is_published()
        {
            MatchSetup solo = new MatchSetup
            {
                SlotOneKind = ControllerKind.Human,
                SlotTwoKind = ControllerKind.Cpu,
            };

            reporter.Publish(Session(), solo);
            Assert.That(published.Number(Global.HumanCount), Is.EqualTo(1f));

            MatchSetup pair = new MatchSetup
            {
                SlotOneKind = ControllerKind.Human,
                SlotTwoKind = ControllerKind.Human,
            };

            reporter.Publish(Session(), pair);
            Assert.That(published.Number(Global.HumanCount), Is.EqualTo(2f));
        }

        [Test]
        public void A_missing_setup_publishes_the_board_without_the_team()
        {
            Assert.DoesNotThrow(() => reporter.Publish(Session(), null));

            Assert.That(published.Has(Global.Coins), Is.True);
            Assert.That(published.Has(Global.Difficulty), Is.False);
        }

        [Test]
        public void A_missing_session_publishes_nothing_rather_than_throwing()
        {
            Assert.DoesNotThrow(() => reporter.Publish(null, new MatchSetup()));

            Assert.That(published.Count, Is.Zero, "there is no board, so there is nothing true to say about one");
        }
    }
}
