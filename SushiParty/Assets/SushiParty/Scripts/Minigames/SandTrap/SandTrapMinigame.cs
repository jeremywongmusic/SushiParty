using SushiParty.Audio;
using SushiParty.Core;
using SushiParty.InputLayer;
using SushiParty.Minigames.Shared;
using SushiParty.Presentation;
using UnityEngine;

namespace SushiParty.Minigames.SandTrap
{
    public sealed class SandTrapMinigame : MinigameController
    {
        private const float HighlightDuration = 1f;
        private const float SwitchTolerance = 0.8f;
        private const float WalkwayOffset = 8f;
        private const float TrackPadding = 1.2f;

        [Header("Tuning")]
        [Tooltip("Seconds Chef Tako takes to step between blocks at Standard skill.")]
        [SerializeField] private float chefStepDuration = 0.42f;
        private SandGrid grid;
        private PoundCharacter rowPlayer;
        private PoundCharacter columnPlayer;
        private CharacterAnimation rowAnimation;
        private CharacterAnimation columnAnimation;
        private Transform[] rowSwitches;
        private Transform[] columnSwitches;
        private int highlightedRow = -1;
        private int highlightedColumn = -1;
        private float rowHighlightTimer;
        private float columnHighlightTimer;
        private Transform chefView;
        private CharacterAnimation chefAnimation;
        private Vector2Int chefTile = new Vector2Int(2, 2);
        private Vector2Int chefTarget = new Vector2Int(2, 2);
        private float chefStepProgress = 1f;
        private float chefThinkTimer;
        private bool chefFalling;
        private float chefFallSpeed;
        private int blocksRemoved;
        public override MinigameId Id => MinigameId.SandTrap;

        public SandTrapSnapshot Snapshot()
        {
            return new SandTrapSnapshot(
                SeatOf(rowPlayer, rowHighlightTimer),
                SeatOf(columnPlayer, columnHighlightTimer),
                chefTarget);
        }

        private static SandTrapSeat SeatOf(PoundCharacter character, float highlightRemaining)
        {
            if (character == null)
            {
                return new SandTrapSeat(false, 0f, false, false, false, highlightRemaining);
            }

            return new SandTrapSeat(
                exists: true,
                trackPosition: character.TrackPosition,
                grounded: character.Current == PoundCharacter.State.Grounded,
                airborne: character.Current == PoundCharacter.State.Airborne,
                pounding: character.IsPounding,
                highlightRemaining: highlightRemaining);
        }

        protected override void OnPrepare()
        {
            Transform arena = new GameObject("Arena").transform;
            arena.SetParent(transform, false);

            grid = new SandGrid(arena);

            BuildPit(arena);
            rowSwitches = BuildWalkway(arena, TrackAxis.Z, P1.Color, "RowWalkway");
            columnSwitches = BuildWalkway(arena, TrackAxis.X, P2.Color, "ColumnWalkway");

            rowPlayer = CreateCharacter(arena, P1, TrackAxis.Z, "Player_Rows", out rowAnimation);
            columnPlayer = CreateCharacter(arena, P2, TrackAxis.X, "Player_Columns", out columnAnimation);

            rowPlayer.Pounded += OnRowPound;
            columnPlayer.Pounded += OnColumnPound;

            chefView = Shapes.Octopus(arena, Palette.TakoPurple, 0.62f, "ChefTako", isChef: true);
            chefView.position = SandGrid.CentreOf(chefTile.x, chefTile.y);
            chefAnimation = CharacterAnimation.Attach(chefView);

            Hud.ConfigurePips(4, "Chef Tako's escape routes");
            RefreshHud();
        }

        private void BuildPit(Transform parent)
        {
            float span = SandGrid.Size * SandGrid.CellSize;

            Shapes.Cube(
                parent,
                new Vector3(0f, -9f, 0f),
                new Vector3(span + 10f, 0.4f, span + 10f),
                new Color(0.08f, 0.07f, 0.09f),
                "PitFloor");
        }

        private Transform[] BuildWalkway(Transform parent, TrackAxis axis, Color color, string name)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);

            float span = SandGrid.Size * SandGrid.CellSize + TrackPadding * 2f;
            bool alongX = axis == TrackAxis.X;

            Vector3 deckPosition = alongX
                ? new Vector3(0f, -0.3f, -WalkwayOffset)
                : new Vector3(-WalkwayOffset, -0.3f, 0f);

            Vector3 deckScale = alongX
                ? new Vector3(span, 0.6f, 2.4f)
                : new Vector3(2.4f, 0.6f, span);

            Shapes.Cube(root, deckPosition, deckScale, new Color(0.24f, 0.26f, 0.32f), "Deck");

            Transform[] switches = new Transform[SandGrid.Size];
            for (int i = 0; i < SandGrid.Size; i++)
            {
                float coordinate = SandGrid.CoordinateFor(i);
                Vector3 position = alongX
                    ? new Vector3(coordinate, 0.02f, -WalkwayOffset)
                    : new Vector3(-WalkwayOffset, 0.02f, coordinate);

                switches[i] = Shapes.Cylinder(
                    root,
                    position,
                    new Vector3(1.5f, 0.06f, 1.5f),
                    Color.Lerp(color, Color.black, 0.35f),
                    $"Switch{i}");
            }

            return switches;
        }

        private PoundCharacter CreateCharacter(
            Transform parent,
            Participant participant,
            TrackAxis axis,
            string name,
            out CharacterAnimation animation)
        {
            Transform view = Shapes.Octopus(parent, participant.Color, 0.85f, name);
            animation = CharacterAnimation.Attach(view);

            Vector3 origin = axis == TrackAxis.X
                ? new Vector3(0f, 0f, -WalkwayOffset)
                : new Vector3(-WalkwayOffset, 0f, 0f);

            float limit = SandGrid.CoordinateFor(SandGrid.Size - 1) + TrackPadding;

            return new PoundCharacter(
                view,
                participant.Input,
                axis,
                origin,
                -limit,
                limit,
                startTrack: 0f);
        }

        private void OnRowPound(float trackPosition)
        {
            GameAudio.Play(Sfx.SandPound);

            int row = SandGrid.IndexNear(trackPosition, SwitchTolerance);
            if (row < 0)
            {
                Hud.SetStatus("Seat 1 missed the switch — line up with a pad before pounding");
                GameAudio.Play(Sfx.SandPoundMissed);
                return;
            }

            highlightedRow = row;
            rowHighlightTimer = HighlightDuration;

            GameAudio.Play(Sfx.SandLineLit, Sfx.AmountParameter, row);
            TryResolveIntersection();
        }

        private void OnColumnPound(float trackPosition)
        {
            GameAudio.Play(Sfx.SandPound);

            int column = SandGrid.IndexNear(trackPosition, SwitchTolerance);
            if (column < 0)
            {
                Hud.SetStatus("Seat 2 missed the switch — line up with a pad before pounding");
                GameAudio.Play(Sfx.SandPoundMissed);
                return;
            }

            highlightedColumn = column;
            columnHighlightTimer = HighlightDuration;
            GameAudio.Play(Sfx.SandLineLit, Sfx.AmountParameter, column);
            TryResolveIntersection();
        }

        private void TryResolveIntersection()
        {
            if (rowHighlightTimer <= 0f || columnHighlightTimer <= 0f)
            {
                return;
            }

            int row = highlightedRow;
            int column = highlightedColumn;

            ClearHighlights();

            if (!grid.Remove(row, column))
            {
                Hud.SetStatus("Nothing left at that intersection");
                GameAudio.PlayAt(Sfx.SandNothingThere, SandGrid.CentreOf(row, column));
                return;
            }

            blocksRemoved++;
            GameAudio.PlayAt(Sfx.SandBlockFall, SandGrid.CentreOf(row, column));

            if (chefTile == new Vector2Int(row, column) || chefTarget == new Vector2Int(row, column))
            {
                DropChef();
                return;
            }

            RefreshHud();
        }

        private void ClearHighlights()
        {
            highlightedRow = -1;
            highlightedColumn = -1;
            rowHighlightTimer = 0f;
            columnHighlightTimer = 0f;
        }

        protected override void OnPlay(float deltaTime)
        {
            bool controls = !chefFalling;

            rowPlayer.Tick(deltaTime, controls);
            columnPlayer.Tick(deltaTime, controls);

            TickHighlights(deltaTime);
            grid.Tick(deltaTime);

            if (chefFalling)
            {
                TickChefFall(deltaTime);
                return;
            }

            TickChef(deltaTime);
            RefreshHud();
        }

        protected override void OnConclude(MinigameOutcome result)
        {
            if (!result.Won)
            {
                return;
            }

            rowAnimation.Cheer();
            columnAnimation.Cheer();
        }

        protected override void OnSettle(float deltaTime, MinigameOutcome result)
        {
            rowPlayer.Tick(deltaTime, controlsEnabled: false);
            columnPlayer.Tick(deltaTime, controlsEnabled: false);
            grid.Tick(deltaTime);

            if (chefFalling)
            {
                chefFallSpeed += 26f * deltaTime;
                chefView.position += Vector3.down * (chefFallSpeed * deltaTime);
            }
        }

        private void TickHighlights(float deltaTime)
        {
            if (rowHighlightTimer > 0f)
            {
                rowHighlightTimer = Mathf.Max(0f, rowHighlightTimer - deltaTime);
                if (rowHighlightTimer == 0f)
                {
                    highlightedRow = -1;
                }
            }

            if (columnHighlightTimer > 0f)
            {
                columnHighlightTimer = Mathf.Max(0f, columnHighlightTimer - deltaTime);
                if (columnHighlightTimer == 0f)
                {
                    highlightedColumn = -1;
                }
            }

            grid.ApplyHighlights(highlightedRow, highlightedColumn);
            PaintSwitches(rowSwitches, highlightedRow, P1.Color);
            PaintSwitches(columnSwitches, highlightedColumn, P2.Color);
        }

        private static void PaintSwitches(Transform[] switches, int litIndex, Color color)
        {
            if (switches == null)
            {
                return;
            }

            for (int i = 0; i < switches.Length; i++)
            {
                bool lit = i == litIndex;
                Shapes.Tint(switches[i], lit ? color : Color.Lerp(color, Color.black, 0.35f), lit);
            }
        }

        private void TickChef(float deltaTime)
        {
            if (!grid.Has(chefTile.x, chefTile.y) && !grid.Has(chefTarget.x, chefTarget.y))
            {
                DropChef();
                return;
            }

            if (chefStepProgress < 1f)
            {
                chefStepProgress = Mathf.Min(1f, chefStepProgress + deltaTime / StepDuration());
                Vector3 from = SandGrid.CentreOf(chefTile.x, chefTile.y);
                Vector3 to = SandGrid.CentreOf(chefTarget.x, chefTarget.y);

                float hop = Mathf.Sin(chefStepProgress * Mathf.PI) * 0.45f;
                chefView.position = Vector3.Lerp(from, to, chefStepProgress) + Vector3.up * hop;

                if (chefStepProgress >= 1f)
                {
                    chefTile = chefTarget;

                    chefAnimation.SetSpeed(0f);
                    chefAnimation.SetGrounded(true);
                    chefAnimation.Land();
                }

                return;
            }

            chefView.position = SandGrid.CentreOf(chefTile.x, chefTile.y);

            if (!Elapsed(ref chefThinkTimer, deltaTime, StepDuration() * 0.6f))
            {
                return;
            }

            ChooseChefStep();
        }

        private float StepDuration()
        {
            return Context.ChefSkill switch
            {
                CpuSkill.Relaxed => chefStepDuration * 1.45f,
                CpuSkill.Sharp => chefStepDuration * 0.75f,
                _ => chefStepDuration,
            };
        }

        private void ChooseChefStep()
        {
            Vector2Int best = chefTile;
            float bestScore = ScoreTile(chefTile, isCurrent: true);

            Vector2Int[] candidates =
            {
                new Vector2Int(chefTile.x + 1, chefTile.y),
                new Vector2Int(chefTile.x - 1, chefTile.y),
                new Vector2Int(chefTile.x, chefTile.y + 1),
                new Vector2Int(chefTile.x, chefTile.y - 1),
            };

            foreach (Vector2Int candidate in candidates)
            {
                if (!grid.Has(candidate.x, candidate.y))
                {
                    continue;
                }

                float score = ScoreTile(candidate, isCurrent: false);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (best == chefTile)
            {
                return;
            }

            chefTarget = best;
            chefStepProgress = 0f;
            GameAudio.PlayAt(Sfx.SandChefStep, SandGrid.CentreOf(best.x, best.y));

            chefAnimation.SetSpeed(1f);
            chefAnimation.SetGrounded(false);
            chefAnimation.Jump();
        }

        private float ScoreTile(Vector2Int tile, bool isCurrent)
        {
            float score = grid.NeighbourCount(tile.x, tile.y);

            if (tile.x == highlightedRow || tile.y == highlightedColumn)
            {
                score -= 2.5f * Competence();
            }

            if (isCurrent)
            {
                score += 0.35f;
            }

            score += Random.Range(0f, 1.6f * (1f - Competence()));
            return score;
        }

        private float Competence()
        {
            return Context.ChefSkill switch
            {
                CpuSkill.Relaxed => 0.35f,
                CpuSkill.Sharp => 1f,
                _ => 0.7f,
            };
        }

        private void DropChef()
        {
            if (chefFalling)
            {
                return;
            }

            chefFalling = true;
            chefFallSpeed = 2f;
            GameAudio.PlayAt(Sfx.SandChefFall, chefView.position);

            chefAnimation.SetSpeed(0f);
            chefAnimation.SetGrounded(false);
            chefAnimation.SetStunned(true);

            ClearHighlights();
            Hud.SetPips(0);
            Hud.SetStatus("Down he goes!");
        }

        private void TickChefFall(float deltaTime)
        {
            chefFallSpeed += 26f * deltaTime;
            chefView.position += Vector3.down * (chefFallSpeed * deltaTime);

            if (chefView.position.y < -4f)
            {
                Win($"Dropped him after removing {blocksRemoved} blocks");
            }
        }

        private void RefreshHud()
        {
            int routes = grid.NeighbourCount(chefTile.x, chefTile.y);
            Hud.SetPips(routes);

            if (rowHighlightTimer > 0f && columnHighlightTimer <= 0f)
            {
                Hud.SetStatus($"Row {highlightedRow + 1} is lit — seat 2 has {rowHighlightTimer:0.0}s to answer");
                return;
            }

            if (columnHighlightTimer > 0f && rowHighlightTimer <= 0f)
            {
                Hud.SetStatus($"Column {highlightedColumn + 1} is lit — seat 1 has {columnHighlightTimer:0.0}s to answer");
                return;
            }

            Hud.SetStatus(routes == 0
                ? "He's cornered — now take the block he's standing on"
                : "Pound together: seat 1 picks the row, seat 2 picks the column");
        }

        private static bool Elapsed(ref float timer, float deltaTime, float interval)
        {
            timer -= deltaTime;
            if (timer > 0f)
            {
                return false;
            }

            timer = interval;
            return true;
        }

        protected override MinigameOutcome OnTimeUp()
        {
            return MinigameOutcome.Lose($"{blocksRemoved} blocks removed, but he kept his footing");
        }

        protected override MinigameTelemetry SampleTelemetry()
        {
            float motion = Mathf.Max(Mathf.Abs(P1.Input.Move.y), Mathf.Abs(P2.Input.Move.x));

            int routes = grid == null ? 4 : grid.NeighbourCount(chefTile.x, chefTile.y);

            return new MinigameTelemetry
            {
                PlayerMotion = motion,
                ChefMotion = chefStepProgress < 1f ? 1f : 0f,
                Objective = 1f - routes / 4f,
            };
        }

        protected override IParticipantInput CreateCpuBrain(Participant participant)
        {
            return new SandTrapPartnerBrain(Snapshot, participant.Slot, participant.Skill);
        }
    }
}
