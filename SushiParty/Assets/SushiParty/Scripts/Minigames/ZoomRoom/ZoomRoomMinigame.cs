using SushiParty.Audio;
using System.Collections.Generic;
using SushiParty.Core;
using SushiParty.InputLayer;
using SushiParty.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace SushiParty.Minigames.ZoomRoom
{
    public sealed class ZoomRoomMinigame : MinigameController
    {
        private const float PlayerRadius = 0.55f;
        private const float PlayerSpeed = 5.6f;
        private const float CatchRadius = 1.15f;
        private const int MazeCellsX = 6;
        private const int MazeCellsY = 6;
        private const int MazeLoops = 7;
        private const float StrideLength = 1.9f;
        private const float WallBumpInterval = 0.45f;
        private const int TentacleCount = 6;

        private sealed class Runner
        {
            public Transform View;
            public CharacterAnimation Animation;
            public Vector2 Position;
            public float StrideRemaining;
            public int StrideFoot;
            public float BumpCooldown;
        }

        private readonly Runner[] players = new Runner[2];
        private MazeGrid maze;
        private Transform arena;
        private Transform chefView;
        private CharacterAnimation chefAnimation;
        private Vector2 chefPosition;
        private Vector2Int chefTile;
        private Vector2Int chefNextTile;
        private Vector2Int chefPreviousTile;
        private float chefStrideRemaining;
        private int chefStrideFoot;
        private int[,] distanceToOne;
        private int[,] distanceToTwo;
        private RectTransform minimap;
        private readonly Image[] minimapDots = new Image[3];
        private bool caught;
        private float catchFlourish;
        private AudioHandle chaseLoop;
        public override MinigameId Id => MinigameId.ZoomRoom;

        public ZoomRoomSnapshot Snapshot()
        {
            return new ZoomRoomSnapshot(
                maze,
                chefPosition,
                players[0]?.Position ?? Vector2.zero,
                players[1]?.Position ?? Vector2.zero);
        }

        protected override void OnPrepare()
        {
            arena = new GameObject("Arena").transform;
            arena.SetParent(transform, false);

            maze = new MazeGrid(MazeCellsX, MazeCellsY, MazeLoops);
            distanceToOne = maze.CreateDistanceBuffer();
            distanceToTwo = maze.CreateDistanceBuffer();

            BuildMazeGeometry();
            SpawnRunners();
            BuildMinimap();

            Hud.ConfigurePips(0);
            Hud.SetStatus("He is faster than either of you — cut him off from opposite sides");
        }

        private void BuildMazeGeometry()
        {
            float floorSpan = Mathf.Max(maze.Width, maze.Height) * MazeGrid.TileSize + 2f;
            Shapes.Cube(
                arena,
                new Vector3(0f, -0.3f, 0f),
                new Vector3(floorSpan, 0.6f, floorSpan),
                new Color(0.16f, 0.17f, 0.22f),
                "Floor");

            Transform walls = new GameObject("Walls").transform;
            walls.SetParent(arena, false);

            for (int x = 0; x < maze.Width; x++)
            {
                for (int y = 0; y < maze.Height; y++)
                {
                    if (maze.IsOpen(x, y))
                    {
                        continue;
                    }

                    Vector2 world = maze.TileToWorld(new Vector2Int(x, y));
                    Shapes.Cube(
                        walls,
                        new Vector3(world.x, 0.6f, world.y),
                        new Vector3(MazeGrid.TileSize, 1.2f, MazeGrid.TileSize),
                        new Color(0.34f, 0.32f, 0.42f),
                        $"Wall_{x}_{y}");
                }
            }
        }

        private void SpawnRunners()
        {
            Vector2Int cornerOne = maze.NearestOpen(new Vector2Int(1, 1));
            Vector2Int cornerTwo = maze.NearestOpen(new Vector2Int(maze.Width - 2, maze.Height - 2));
            Vector2Int centre = maze.NearestOpen(new Vector2Int(maze.Width / 2, maze.Height / 2));

            players[0] = CreateRunner(P1.Color, maze.TileToWorld(cornerOne), "Player_One");
            players[1] = CreateRunner(P2.Color, maze.TileToWorld(cornerTwo), "Player_Two");

            chefTile = centre;
            chefNextTile = centre;
            chefPreviousTile = centre;
            chefPosition = maze.TileToWorld(centre);

            chefView = Shapes.Octopus(arena, Palette.TakoPurple, 0.5f, "ChefTako", isChef: true);
            chefView.position = new Vector3(chefPosition.x, 0f, chefPosition.y);

            chefAnimation = CharacterAnimation.Attach(chefView);
        }

        private Runner CreateRunner(Color color, Vector2 position, string name)
        {
            Transform view = Shapes.Octopus(arena, color, 0.5f, name);
            view.position = new Vector3(position.x, 0f, position.y);
            return new Runner
            {
                View = view,
                Animation = CharacterAnimation.Attach(view),
                Position = position,
            };
        }

        private void BuildMinimap()
        {
            Canvas canvas = UiKit.CreateCanvas("ZoomRoomMinimap", 120);
            canvas.transform.SetParent(transform, false);

            minimap = UiKit.Rect(canvas.transform, "Minimap");
            minimap.Pin(new Vector2(1f, 0f), new Vector2(260f, 260f), new Vector2(-40f, 40f));
            UiKit.Panel(minimap, "Bg", new Color(0.05f, 0.06f, 0.10f, 0.80f)).Rt().Stretch();

            UiKit.Label(minimap, "RADAR", 18, TextAnchor.UpperCenter, UiKit.InkDim)
                .Rt().Pin(new Vector2(0.5f, 1f), new Vector2(240f, 24f), new Vector2(0f, -6f));

            minimapDots[0] = CreateDot(P1.Color, 16f);
            minimapDots[1] = CreateDot(P2.Color, 16f);
            minimapDots[2] = CreateDot(Palette.TakoPurple, 20f);
        }

        private Image CreateDot(Color color, float size)
        {
            Image dot = UiKit.Panel(minimap, "Dot", color);
            dot.Rt().Pin(new Vector2(0.5f, 0.5f), new Vector2(size, size), Vector2.zero);
            return dot;
        }

        protected override void OnBegin()
        {
            chaseLoop = GameAudio.Loop(Sfx.ZoomChaseLoop);
        }

        protected override void OnConclude(MinigameOutcome result)
        {
            GameAudio.Stop(ref chaseLoop);

            foreach (Runner runner in players)
            {
                runner.Animation.SetSpeed(0f);

                if (result.Won)
                {
                    runner.Animation.Cheer();
                }
            }
        }

        protected override void OnPlay(float deltaTime)
        {
            MovePlayer(players[0], P1.Input, deltaTime);
            MovePlayer(players[1], P2.Input, deltaTime);
            MoveChef(deltaTime);
            UpdateMinimap();
            UpdateChaseTension();

            for (int i = 0; i < players.Length; i++)
            {
                if (Vector2.Distance(players[i].Position, chefPosition) < CatchRadius)
                {
                    caught = true;
                    Hud.SetStatus("Got him!");

                    chefAnimation.SetSpeed(0f);
                    chefAnimation.SetStunned(true);

                    GameAudio.PlayAt(Sfx.ZoomCatch, new Vector3(chefPosition.x, 0f, chefPosition.y));
                    Win("Cornered and caught");
                    return;
                }
            }
        }

        private void UpdateChaseTension()
        {
            float nearest = float.MaxValue;
            foreach (Runner runner in players)
            {
                nearest = Mathf.Min(nearest, Vector2.Distance(runner.Position, chefPosition));
            }

            GameAudio.SetParameter(
                chaseLoop,
                Sfx.ProximityParameter,
                Mathf.Clamp01(Mathf.InverseLerp(14f, 1.5f, nearest)));
        }

        protected override void OnSettle(float deltaTime, MinigameOutcome result)
        {
            if (!caught)
            {
                return;
            }

            catchFlourish += deltaTime;
            chefView.localRotation = Quaternion.Euler(0f, catchFlourish * 540f, 0f);
        }

        private void MovePlayer(Runner runner, IParticipantInput input, float deltaTime)
        {
            Vector2 move = Vector2.ClampMagnitude(input.Move, 1f);
            float step = PlayerSpeed * deltaTime;
            Vector2 previous = runner.Position;
            runner.Position = maze.Move(runner.Position, move * step, PlayerRadius);
            runner.View.position = new Vector3(runner.Position.x, 0f, runner.Position.y);

            float travelled = Vector2.Distance(previous, runner.Position);

            runner.Animation.SetSpeed(step > 0f ? travelled / step : 0f);

            Footstep(runner.View, ref runner.StrideRemaining, ref runner.StrideFoot, travelled);
            WallBump(runner, move.magnitude, step > 0f ? travelled / step : 1f);

            if (move.sqrMagnitude > 0.01f)
            {
                runner.View.rotation = Quaternion.LookRotation(new Vector3(move.x, 0f, move.y), Vector3.up);
            }
        }

        private static void Footstep(Transform view, ref float remaining, ref int foot, float distance)
        {
            remaining -= distance;
            if (remaining > 0f)
            {
                return;
            }

            remaining = StrideLength;
            foot = (foot + 1) % TentacleCount;
            GameAudio.PlayAt(Sfx.CharacterFootstep, view.position, Sfx.StepParameter, foot);
        }

        private void WallBump(Runner runner, float asked, float got)
        {
            runner.BumpCooldown -= Time.deltaTime;

            if (asked < 0.4f || got > 0.35f || runner.BumpCooldown > 0f)
            {
                return;
            }

            runner.BumpCooldown = WallBumpInterval;

            GameAudio.PlayAt(Sfx.ZoomWallBump, runner.View.position, Sfx.ForceParameter,
                Mathf.Clamp01(asked * (1f - got)));
        }

        private float ChefSpeed()
        {
            return Context.ChefSkill switch
            {
                CpuSkill.Relaxed => 6.3f,
                CpuSkill.Sharp => 8.1f,
                _ => 7.2f,
            };
        }

        private void MoveChef(float deltaTime)
        {
            Vector2 previous = chefPosition;
            Vector2 targetWorld = maze.TileToWorld(chefNextTile);
            Vector2 toTarget = targetWorld - chefPosition;
            float step = ChefSpeed() * deltaTime;

            if (toTarget.magnitude <= step)
            {
                chefPosition = targetWorld;
                chefPreviousTile = chefTile;
                chefTile = chefNextTile;
                ChooseChefRoute();
            }
            else
            {
                chefPosition += toTarget.normalized * step;
            }

            chefView.position = new Vector3(chefPosition.x, 0f, chefPosition.y);

            float travelled = Vector2.Distance(previous, chefPosition);

            chefAnimation.SetSpeed(step > 0f ? travelled / step : 0f);

            Footstep(chefView, ref chefStrideRemaining, ref chefStrideFoot, travelled);

            if (toTarget.sqrMagnitude > 0.001f)
            {
                chefView.rotation = Quaternion.LookRotation(new Vector3(toTarget.x, 0f, toTarget.y), Vector3.up);
            }
        }

        private void ChooseChefRoute()
        {
            maze.FillDistances(maze.WorldToTile(players[0].Position), distanceToOne);
            maze.FillDistances(maze.WorldToTile(players[1].Position), distanceToTwo);

            float bestScore = float.MinValue;
            Vector2Int best = chefTile;

            foreach (Vector2Int candidate in maze.OpenNeighbours(chefTile))
            {
                int one = distanceToOne[candidate.x, candidate.y];
                int two = distanceToTwo[candidate.x, candidate.y];
                if (one == int.MaxValue || two == int.MaxValue)
                {
                    continue;
                }

                float score = Mathf.Min(one, two);

                score += (one + two) * 0.08f;

                if (candidate == chefPreviousTile)
                {
                    score -= 1.6f;
                }

                score += Random.Range(0f, Sloppiness());

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (best != chefNextTile)
            {
                GameAudio.PlayAt(Sfx.ZoomChefTurn, new Vector3(chefPosition.x, 0f, chefPosition.y));
            }

            chefNextTile = best;
        }

        private float Sloppiness()
        {
            return Context.ChefSkill switch
            {
                CpuSkill.Relaxed => 2.2f,
                CpuSkill.Sharp => 0.15f,
                _ => 0.9f,
            };
        }

        private void UpdateMinimap()
        {
            float extentX = maze.Width * MazeGrid.TileSize * 0.5f;
            float extentY = maze.Height * MazeGrid.TileSize * 0.5f;
            const float half = 110f;

            PlaceDot(minimapDots[0], players[0].Position, extentX, extentY, half);
            PlaceDot(minimapDots[1], players[1].Position, extentX, extentY, half);
            PlaceDot(minimapDots[2], chefPosition, extentX, extentY, half);
        }

        private static void PlaceDot(Image dot, Vector2 world, float extentX, float extentY, float half)
        {
            dot.Rt().anchoredPosition = new Vector2(
                Mathf.Clamp(world.x / extentX, -1f, 1f) * half,
                Mathf.Clamp(world.y / extentY, -1f, 1f) * half);
        }

        protected override MinigameOutcome OnTimeUp()
        {
            float closest = float.MaxValue;
            foreach (Runner runner in players)
            {
                closest = Mathf.Min(closest, Vector2.Distance(runner.Position, chefPosition));
            }

            return MinigameOutcome.Lose($"He stayed {closest:0.0} units clear at the buzzer");
        }

        protected override MinigameTelemetry SampleTelemetry()
        {
            float nearest = float.MaxValue;
            foreach (Runner runner in players)
            {
                nearest = Mathf.Min(nearest, Vector2.Distance(runner.Position, chefPosition));
            }

            return new MinigameTelemetry
            {
                PlayerMotion = Mathf.Max(
                    Vector2.ClampMagnitude(P1.Input.Move, 1f).magnitude,
                    Vector2.ClampMagnitude(P2.Input.Move, 1f).magnitude),

                ChefMotion = caught ? 0f : 1f,
                Objective = Mathf.InverseLerp(16f, CatchRadius, nearest),
            };
        }

        protected override IParticipantInput CreateCpuBrain(Participant participant)
        {
            return new ZoomRoomBrain(Snapshot, participant.Slot, participant.Skill);
        }
    }

    public sealed class ZoomRoomBrain : CpuBrain<ZoomRoomSnapshot>
    {
        private const int InterceptMin = 3;
        private const int InterceptMax = 7;
        private readonly ParticipantSlot slot;
        private readonly ParticipantSlot partnerSlot;
        private int[,] fromChef;
        private int[,] toTarget;
        private float retargetTimer;
        private Vector2Int targetTile;

        public ZoomRoomBrain(System.Func<ZoomRoomSnapshot> world, ParticipantSlot slot, CpuSkill skill)
            : base(world, skill)
        {
            this.slot = slot;
            partnerSlot = slot == ParticipantSlot.One ? ParticipantSlot.Two : ParticipantSlot.One;
        }

        protected override void Think(in ZoomRoomSnapshot world, float deltaTime)
        {
            MazeGrid maze = world.Maze;
            if (maze == null)
            {
                Steer(Vector2.zero);
                return;
            }

            fromChef ??= maze.CreateDistanceBuffer();
            toTarget ??= maze.CreateDistanceBuffer();

            Vector2 self = world.PlayerPosition(slot);

            if (Elapsed(ref retargetTimer, deltaTime, Mathf.Max(0.25f, ReactionDelay)))
            {
                ChooseInterception(in world, maze, self);
                maze.FillDistances(targetTile, toTarget);
            }

            Vector2Int selfTile = maze.NearestOpen(maze.WorldToTile(self));
            Vector2 goal = maze.TileToWorld(selfTile);
            int bestDistance = toTarget[selfTile.x, selfTile.y];

            foreach (Vector2Int neighbour in maze.OpenNeighbours(selfTile))
            {
                int distance = toTarget[neighbour.x, neighbour.y];
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    goal = maze.TileToWorld(neighbour);
                }
            }

            Vector2 delta = goal - self;
            Steer(delta.sqrMagnitude < 0.02f ? Vector2.zero : delta.normalized);
        }

        private void ChooseInterception(in ZoomRoomSnapshot world, MazeGrid maze, Vector2 self)
        {
            Vector2Int chefTile = maze.NearestOpen(maze.WorldToTile(world.ChefPosition));
            maze.FillDistances(chefTile, fromChef);

            Vector2 partner = world.PlayerPosition(partnerSlot);

            Vector2Int selfTile = maze.NearestOpen(maze.WorldToTile(self));
            if (fromChef[selfTile.x, selfTile.y] <= 2)
            {
                targetTile = chefTile;
                return;
            }

            float bestScore = float.MinValue;
            targetTile = chefTile;

            for (int x = 0; x < maze.Width; x++)
            {
                for (int y = 0; y < maze.Height; y++)
                {
                    if (!maze.IsOpen(x, y))
                    {
                        continue;
                    }

                    int ahead = fromChef[x, y];
                    if (ahead < InterceptMin || ahead > InterceptMax)
                    {
                        continue;
                    }

                    Vector2 spot = maze.TileToWorld(new Vector2Int(x, y));

                    float spread = Vector2.Distance(spot, partner);

                    float reach = -Vector2.Distance(spot, self) * 0.55f;

                    float score = spread + reach + Random.Range(0f, (1f - Competence) * 6f);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        targetTile = new Vector2Int(x, y);
                    }
                }
            }
        }
    }
}
