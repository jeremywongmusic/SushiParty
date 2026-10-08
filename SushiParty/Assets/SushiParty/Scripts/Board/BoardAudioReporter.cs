using SushiParty.Audio;
using SushiParty.Core;
using UnityEngine;

namespace SushiParty.Board
{
    public sealed class BoardAudioReporter
    {
        private readonly IGlobalParameters globals;
        private int lastCoinTotal;
        private bool seenASession;

        public BoardAudioReporter(IGlobalParameters globals)
        {
            this.globals = globals;
        }

        public void Publish(BoardSession session, MatchSetup setup)
        {
            if (session == null)
            {
                return;
            }

            int coins = CoinsHeldByThePair(session);

            int swing = seenASession ? coins - lastCoinTotal : 0;
            lastCoinTotal = coins;
            seenASession = true;

            Publish(AudioParameter.Number(Global.Coins, coins));
            Publish(AudioParameter.Number(Global.CoinSwing, swing));

            Publish(AudioParameter.Number(Global.BoardRound, session.Round));
            Publish(AudioParameter.Number(Global.BoardProgress, Progress(session)));

            if (setup != null)
            {
                Publish(AudioParameter.Labelled(Global.Difficulty, setup.ChefSkill.ToString()));
                Publish(AudioParameter.Number(Global.HumanCount, HumanSeats(setup)));
            }
        }

        private void Publish(AudioParameter parameter)
        {
            globals?.Publish(parameter);
        }

        public void Reset()
        {
            lastCoinTotal = 0;
            seenASession = false;
        }

        public static int CoinsHeldByThePair(BoardSession session)
        {
            if (session == null)
            {
                return 0;
            }

            return session.TokenFor(BoardSeat.One).Coins + session.TokenFor(BoardSeat.Two).Coins;
        }

        public static float Progress(BoardSession session)
        {
            if (session == null || session.TotalRounds <= 0)
            {
                return 0f;
            }

            return Mathf.Clamp01((session.Round - 1) / (float)session.TotalRounds);
        }

        private static int HumanSeats(MatchSetup setup)
        {
            int seats = 0;

            if (setup.SlotOneKind == ControllerKind.Human)
            {
                seats++;
            }

            if (setup.SlotTwoKind == ControllerKind.Human)
            {
                seats++;
            }

            return seats;
        }
    }
}
