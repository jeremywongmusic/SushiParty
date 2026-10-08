using SushiParty.Core;

namespace SushiParty.Board
{
    public enum BoardSeat
    {
        One = 0,
        Two = 1,
        Chef = 2,
    }

    public sealed class BoardToken
    {
        public BoardToken(BoardSeat seat, string displayName)
        {
            Seat = seat;
            DisplayName = displayName;
        }

        public BoardSeat Seat { get; }
        public string DisplayName { get; }

        public int Space { get; internal set; }

        public int Coins { get; internal set; }

        public int GoldenCoins { get; internal set; }

        public int SkipTurns { get; internal set; }

        public int TotalSteps { get; internal set; }

        public bool IsPlayer => Seat != BoardSeat.Chef;
    }

    public readonly struct TurnPlan
    {
        public readonly BoardSeat Seat;
        public readonly bool Skipped;
        public readonly int Rolled;
        public readonly int From;

        public TurnPlan(BoardSeat seat, bool skipped, int rolled, int from)
        {
            Seat = seat;
            Skipped = skipped;
            Rolled = rolled;
            From = from;
        }
    }

    public readonly struct TurnReport
    {
        public readonly BoardSeat Seat;
        public readonly bool Skipped;
        public readonly int Rolled;
        public readonly int From;
        public readonly int To;
        public readonly SpaceKind Landed;
        public readonly int CoinDelta;
        public readonly bool ClaimedGoldenCoin;
        public readonly bool Swapped;
        public readonly BoardSeat SwappedWith;
        public readonly bool LostNextTurn;

        public TurnReport(
            BoardSeat seat,
            bool skipped,
            int rolled,
            int from,
            int to,
            SpaceKind landed,
            int coinDelta,
            bool claimedGoldenCoin,
            bool swapped,
            BoardSeat swappedWith,
            bool lostNextTurn)
        {
            Seat = seat;
            Skipped = skipped;
            Rolled = rolled;
            From = from;
            To = to;
            Landed = landed;
            CoinDelta = coinDelta;
            ClaimedGoldenCoin = claimedGoldenCoin;
            Swapped = swapped;
            SwappedWith = swappedWith;
            LostNextTurn = lostNextTurn;
        }
    }

    public readonly struct BoardResult
    {
        public readonly bool PairWon;
        public readonly int PairGoldenCoins;
        public readonly int ChefGoldenCoins;
        public readonly int PairCoins;
        public readonly int ChefCoins;

        public BoardResult(bool pairWon, int pairGoldenCoins, int chefGoldenCoins, int pairCoins, int chefCoins)
        {
            PairWon = pairWon;
            PairGoldenCoins = pairGoldenCoins;
            ChefGoldenCoins = chefGoldenCoins;
            PairCoins = pairCoins;
            ChefCoins = chefCoins;
        }
    }

    public sealed class BoardSession
    {
        public const int ShrinePrice = 10;
        public const int CoinGainAmount = 3;
        public const int CoinLossAmount = 3;
        private const int RollFromDie = 0;
        private readonly IDie die;
        private readonly BoardToken[] tokens;
        private bool turnInProgress;
        private TurnPlan pending;
        private int chosenExit = -1;

        public BoardSession(BoardLayout layout, IDie die, int totalRounds = 10, int startingCoins = 5)
        {
            Layout = layout ?? throw new System.ArgumentNullException(nameof(layout));
            this.die = die ?? throw new System.ArgumentNullException(nameof(die));

            if (totalRounds < 1)
            {
                throw new System.ArgumentException("a board game needs at least one round", nameof(totalRounds));
            }

            TotalRounds = totalRounds;

            tokens = new[]
            {
                new BoardToken(BoardSeat.One, "Player 1"),
                new BoardToken(BoardSeat.Two, "Player 2"),
                new BoardToken(BoardSeat.Chef, "Chef Tako"),
            };

            for (int i = 0; i < tokens.Length; i++)
            {
                tokens[i].Coins = startingCoins;
            }

            Round = 1;
        }

        public BoardLayout Layout { get; }

        public int TotalRounds { get; }

        public int Round { get; private set; }

        public BoardSeat Turn { get; private set; } = BoardSeat.One;

        public bool AwaitingMinigame { get; private set; }

        public bool Finished { get; private set; }

        public BoardToken TokenFor(BoardSeat seat) => tokens[(int)seat];
        public System.Collections.Generic.IReadOnlyList<BoardToken> Tokens => tokens;

        public int ShrineSpace
        {
            get
            {
                for (int i = 0; i < Layout.Count; i++)
                {
                    if (Layout.KindAt(i) == SpaceKind.Shrine)
                    {
                        return i;
                    }
                }

                return -1;
            }
        }

        public int StepsRemaining { get; private set; }

        public bool AwaitingChoice =>
            turnInProgress && StepsRemaining > 0 && chosenExit < 0 && Layout.IsJunction(TokenFor(pending.Seat).Space);

        public System.Collections.Generic.IReadOnlyList<int> Choices =>
            Layout.ExitsOf(TokenFor(pending.Seat).Space);

        public TurnPlan BeginTurn()
        {
            return Begin(RollFromDie);
        }

        public TurnPlan BeginTurn(int rolled)
        {
            if (rolled < 1 || rolled > die.Faces)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(rolled), rolled, $"a stopped face has to be within 1..{die.Faces}");
            }

            return Begin(rolled);
        }

        private TurnPlan Begin(int supplied)
        {
            if (Finished || AwaitingMinigame)
            {
                throw new System.InvalidOperationException("the round is not accepting turns");
            }

            if (turnInProgress)
            {
                throw new System.InvalidOperationException("a turn is already in progress");
            }

            BoardToken token = TokenFor(Turn);
            turnInProgress = true;
            chosenExit = -1;

            if (token.SkipTurns > 0)
            {
                StepsRemaining = 0;
                pending = new TurnPlan(Turn, skipped: true, rolled: 0, from: token.Space);
                return pending;
            }

            int rolled = supplied == RollFromDie ? die.Roll() : supplied;
            StepsRemaining = rolled;
            pending = new TurnPlan(Turn, skipped: false, rolled: rolled, from: token.Space);
            return pending;
        }

        public void ChooseExit(int spaceIndex)
        {
            if (!AwaitingChoice)
            {
                throw new System.InvalidOperationException("nothing is being chosen right now");
            }

            System.Collections.Generic.IReadOnlyList<int> options = Choices;
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i] == spaceIndex)
                {
                    chosenExit = spaceIndex;
                    return;
                }
            }

            throw new System.ArgumentException($"space {spaceIndex} is not a way on from here", nameof(spaceIndex));
        }

        public bool StepOnce()
        {
            if (!turnInProgress)
            {
                throw new System.InvalidOperationException("no turn is in progress");
            }

            if (StepsRemaining <= 0)
            {
                throw new System.InvalidOperationException("the move is already finished");
            }

            if (AwaitingChoice)
            {
                throw new System.InvalidOperationException("this fork has not been answered");
            }

            BoardToken token = TokenFor(pending.Seat);
            System.Collections.Generic.IReadOnlyList<int> onward = Layout.ExitsOf(token.Space);

            token.Space = chosenExit >= 0 ? chosenExit : onward[0];
            token.TotalSteps++;
            chosenExit = -1;
            StepsRemaining--;

            return StepsRemaining > 0;
        }

        public TurnReport TakeTurn(System.Func<System.Collections.Generic.IReadOnlyList<int>, int> chooseExit = null)
        {
            BeginTurn();

            while (StepsRemaining > 0)
            {
                if (AwaitingChoice)
                {
                    System.Collections.Generic.IReadOnlyList<int> options = Choices;
                    ChooseExit(chooseExit != null ? chooseExit(options) : options[0]);
                }

                StepOnce();
            }

            return CompleteTurn();
        }

        public TurnReport CompleteTurn()
        {
            if (!turnInProgress)
            {
                throw new System.InvalidOperationException("no turn is in progress");
            }

            if (StepsRemaining > 0)
            {
                throw new System.InvalidOperationException("the move is not finished");
            }

            turnInProgress = false;
            chosenExit = -1;
            BoardToken token = TokenFor(pending.Seat);

            if (pending.Skipped)
            {
                token.SkipTurns--;
                AdvanceTurn();
                return new TurnReport(
                    pending.Seat, true, 0, pending.From, token.Space,
                    Layout.KindAt(token.Space), 0, false, false, pending.Seat, false);
            }

            SpaceKind landed = Layout.KindAt(token.Space);
            int coinsBefore = token.Coins;
            bool claimed = false;
            bool swapped = false;
            BoardSeat swappedWith = pending.Seat;
            bool lostNextTurn = false;

            switch (landed)
            {
                case SpaceKind.CoinGain:
                    token.Coins += CoinGainAmount;
                    break;

                case SpaceKind.CoinLoss:
                    token.Coins = Subtract(token.Coins, CoinLossAmount);
                    break;

                case SpaceKind.Shrine:
                    if (token.Coins >= ShrinePrice)
                    {
                        token.Coins -= ShrinePrice;
                        token.GoldenCoins++;
                        claimed = true;
                        RelocateShrine(token.Space);
                    }

                    break;

                case SpaceKind.Wasabi:
                    token.SkipTurns += 1;
                    lostNextTurn = true;
                    break;

                case SpaceKind.Swap:
                    BoardToken other = Furthest(pending.Seat);
                    if (other != null)
                    {
                        (token.Space, other.Space) = (other.Space, token.Space);
                        swapped = true;
                        swappedWith = other.Seat;
                    }

                    break;
            }

            AdvanceTurn();

            return new TurnReport(
                pending.Seat,
                false,
                pending.Rolled,
                pending.From,
                token.Space,
                landed,
                token.Coins - coinsBefore,
                claimed,
                swapped,
                swappedWith,
                lostNextTurn);
        }

        public void ApplyMinigameOutcome(bool pairWon)
        {
            if (!AwaitingMinigame)
            {
                throw new System.InvalidOperationException("no minigame is owed");
            }

            int stake = MinigameLibrary.CoinStake;
            BoardToken chef = TokenFor(BoardSeat.Chef);

            for (int i = 0; i < tokens.Length; i++)
            {
                BoardToken token = tokens[i];
                if (!token.IsPlayer)
                {
                    continue;
                }

                if (pairWon)
                {
                    token.Coins += stake;
                }
                else
                {
                    int taken = token.Coins < stake ? token.Coins : stake;
                    token.Coins -= taken;
                    chef.Coins += taken;
                }
            }

            AwaitingMinigame = false;

            if (Round >= TotalRounds)
            {
                Finished = true;
                return;
            }

            Round++;
            Turn = BoardSeat.One;
        }

        public BoardResult Result()
        {
            BoardToken one = TokenFor(BoardSeat.One);
            BoardToken two = TokenFor(BoardSeat.Two);
            BoardToken chef = TokenFor(BoardSeat.Chef);

            int pairGolden = one.GoldenCoins + two.GoldenCoins;
            int pairCoins = one.Coins + two.Coins;

            bool pairWon = pairGolden > chef.GoldenCoins
                           || (pairGolden == chef.GoldenCoins && pairCoins > chef.Coins);

            return new BoardResult(pairWon, pairGolden, chef.GoldenCoins, pairCoins, chef.Coins);
        }

        private void AdvanceTurn()
        {
            if (Turn == BoardSeat.Chef)
            {
                AwaitingMinigame = true;
                return;
            }

            Turn = Turn == BoardSeat.One ? BoardSeat.Two : BoardSeat.Chef;
        }

        private void RelocateShrine(int from)
        {
            Layout.SetKind(from, SpaceKind.CoinGain);

            int offset = Layout.Count / 3 + die.Roll();
            int destination = Layout.Advance(from, offset);

            if (Layout.KindAt(destination) == SpaceKind.Start)
            {
                destination = Layout.Advance(destination, 1);
            }

            Layout.SetKind(destination, SpaceKind.Shrine);
        }

        private BoardToken Furthest(BoardSeat asking)
        {
            BoardToken best = null;
            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokens[i].Seat == asking)
                {
                    continue;
                }

                if (best == null || tokens[i].TotalSteps > best.TotalSteps)
                {
                    best = tokens[i];
                }
            }

            return best;
        }

        private static int Subtract(int value, int amount)
        {
            int result = value - amount;
            return result < 0 ? 0 : result;
        }
    }
}
