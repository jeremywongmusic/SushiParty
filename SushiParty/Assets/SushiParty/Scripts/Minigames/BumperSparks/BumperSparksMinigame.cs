using SushiParty.Audio;
using System.Collections.Generic;
using SushiParty.Core;
using SushiParty.InputLayer;
using SushiParty.Presentation;
using UnityEngine;

namespace SushiParty.Minigames.BumperSparks
{
    public sealed class BumperSparksMinigame : MinigameController
    {
        private const int HitsToWin = 3;
        private const float PlayerStunDuration = 1.1f;
        private const float ChefStunDuration = 0f;
        private const float ChefInvulnerability = 1.8f;
        private const float RingBounceRetention = 0.55f;
        private const int RingSegments = 64;

        [Header("Arena")]
        [Tooltip("Radius of the platform. The electrified ring sits on this edge.")]
        [SerializeField] private float arenaRadius = 9f;

        [Tooltip("Optional authored spawn markers. Falls back to a default layout if absent.")]
        [SerializeField] private Transform spawnPlayerOne;
        [SerializeField] private Transform spawnPlayerTwo;
        [SerializeField] private Transform spawnChef;
        private readonly List<BumperActor> players = new List<BumperActor>();
        private readonly List<BumperActor> everyone = new List<BumperActor>();
        private BumperActor chef;
        private ChefSparksBrain chefBrain;
        private Transform arenaRoot;
        private int hits;
        public override MinigameId Id => MinigameId.BumperSparks;

        public BumperSparksSnapshot Snapshot()
        {
            return new BumperSparksSnapshot(
                arenaRadius,
                Describe(chef),
                Describe(PlayerAt(0)),
                Describe(PlayerAt(1)));
        }

        private BumperActor PlayerAt(int index)
        {
            return index >= 0 && index < players.Count ? players[index] : null;
        }

        private static BumperVehicle Describe(BumperActor actor)
        {
            return actor == null
                ? default
                : new BumperVehicle(actor.Position, actor.Radius, actor.IsStunned);
        }

        protected override void OnPrepare()
        {
            arenaRoot = new GameObject("Arena").transform;
            arenaRoot.SetParent(transform, false);

            BuildPlatform();
            BuildRing();
            SpawnActors();

            Hud.ConfigurePips(HitsToWin, "Chef Tako knocked into the ring");
            Hud.SetPips(0);
            UpdateStatus();
        }

        private void BuildPlatform()
        {
            Shapes.Cylinder(
                arenaRoot,
                new Vector3(0f, -0.12f, 0f),
                new Vector3(arenaRadius * 2f, 0.12f, arenaRadius * 2f),
                new Color(0.16f, 0.18f, 0.24f),
                "Platform");

            Shapes.Cylinder(
                arenaRoot,
                new Vector3(0f, -0.02f, 0f),
                new Vector3(arenaRadius * 1.3f, 0.02f, arenaRadius * 1.3f),
                new Color(0.20f, 0.23f, 0.31f),
                "InnerDisc");
        }

        private void BuildRing()
        {
            Transform ring = new GameObject("ElectricRing").transform;
            ring.SetParent(arenaRoot, false);

            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i / (float)RingSegments * Mathf.PI * 2f;
                Vector3 position = new Vector3(Mathf.Cos(angle), 0.45f, Mathf.Sin(angle)) * 1f;
                position.x *= arenaRadius + 0.32f;
                position.z *= arenaRadius + 0.32f;
                position.y = 0.45f;

                Transform segment = Shapes.Create(
                    PrimitiveType.Cube,
                    ring,
                    position,
                    new Vector3(0.62f, 0.9f, 0.3f),
                    Palette.Danger,
                    $"Segment{i}",
                    glow: true);

                segment.rotation = Quaternion.LookRotation(new Vector3(position.x, 0f, position.z), Vector3.up);
            }
        }

        private void SpawnActors()
        {
            players.Clear();
            everyone.Clear();

            Vector2 p1Spawn = ReadSpawn(spawnPlayerOne, new Vector2(-arenaRadius * 0.45f, -arenaRadius * 0.35f));
            Vector2 p2Spawn = ReadSpawn(spawnPlayerTwo, new Vector2(arenaRadius * 0.45f, -arenaRadius * 0.35f));
            Vector2 chefSpawn = ReadSpawn(spawnChef, new Vector2(0f, arenaRadius * 0.35f));

            players.Add(CreateActor(P1.Input, P1.Color, p1Spawn, "Vehicle_P1", isChef: false));
            players.Add(CreateActor(P2.Input, P2.Color, p2Spawn, "Vehicle_P2", isChef: false));

            chefBrain = new ChefSparksBrain(Snapshot, Context.ChefSkill);
            chef = CreateActor(chefBrain, Palette.TakoPurple, chefSpawn, "Vehicle_ChefTako", isChef: true);

            chef.MaxSpeed = 9.7f;
            chef.Acceleration = 36f;

            everyone.AddRange(players);
            everyone.Add(chef);
        }

        private BumperActor CreateActor(IParticipantInput input, Color color, Vector2 spawn, string name, bool isChef)
        {
            Transform view = Shapes.Octopus(arenaRoot, color, 0.85f, name, isChef);

            BumperActor actor = new BumperActor(view, input, color, isChef);
            actor.Place(spawn);
            return actor;
        }

        private static Vector2 ReadSpawn(Transform marker, Vector2 fallback)
        {
            if (marker == null)
            {
                return fallback;
            }

            Vector3 p = marker.position;
            return new Vector2(p.x, p.z);
        }

        protected override void OnPlay(float deltaTime)
        {
            Simulate(deltaTime, controlsEnabled: true);

            if (hits >= HitsToWin)
            {
                Win($"Chef Tako rang the bell {HitsToWin} times");
            }
        }

        protected override void OnSettle(float deltaTime, MinigameOutcome result)
        {
            Simulate(deltaTime, controlsEnabled: false);
        }

        private void Simulate(float deltaTime, bool controlsEnabled)
        {
            chefBrain.Tick(deltaTime);

            foreach (BumperActor actor in everyone)
            {
                actor.Integrate(deltaTime, controlsEnabled);
            }

            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < everyone.Count; i++)
                {
                    for (int j = i + 1; j < everyone.Count; j++)
                    {
                        BumperActor.Resolve(everyone[i], everyone[j]);
                    }
                }
            }

            foreach (BumperActor player in players)
            {
                bool wasStunned = player.IsStunned;

                if (player.ClampToArena(arenaRadius, RingBounceRetention) && !player.IsStunned)
                {
                    player.Stun(PlayerStunDuration);
                    GameAudio.PlayAt(Sfx.BumperRingZap, player.WorldPosition);
                    wasStunned = true;
                }

                if (wasStunned && !player.IsStunned)
                {
                    GameAudio.PlayAt(Sfx.BumperStunRecover, player.WorldPosition);
                }
            }

            if (chef.ClampToArena(arenaRadius, RingBounceRetention)
                && controlsEnabled
                && chef.TryHit(ChefStunDuration, ChefInvulnerability))
            {
                RegisterHit();
            }

            foreach (BumperActor actor in everyone)
            {
                actor.SyncView();

                GameAudio.SetPosition(actor.Engine, actor.WorldPosition);
                GameAudio.SetParameter(
                    actor.Engine,
                    Sfx.SpeedParameter,
                    Mathf.Clamp01(actor.Velocity.magnitude / actor.MaxSpeed));
            }
        }

        protected override void OnBegin()
        {
            foreach (BumperActor actor in everyone)
            {
                actor.Engine = GameAudio.LoopAt(Sfx.BumperEngineLoop, actor.WorldPosition);
            }
        }

        protected override void OnConclude(MinigameOutcome result)
        {
            foreach (BumperActor actor in everyone)
            {
                GameAudio.Stop(ref actor.Engine);
            }

            if (!result.Won)
            {
                return;
            }

            foreach (BumperActor player in players)
            {
                player.Cheer();
            }
        }

        private void RegisterHit()
        {
            hits++;
            Hud.SetPips(hits);

            GameAudio.PlayAt(Sfx.BumperChefRinged, chef.WorldPosition);

            chef.Place(Vector2.zero);
            GameAudio.Play(Sfx.BumperChefRespawn);
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            int remaining = Mathf.Max(0, HitsToWin - hits);
            Hud.SetStatus(remaining > 0
                ? $"{remaining} more {(remaining == 1 ? "hit" : "hits")} — shove him from the inside, and stay off the ring yourself"
                : "That's three!");
        }

        protected override MinigameOutcome OnTimeUp()
        {
            return MinigameOutcome.Lose($"{hits} of {HitsToWin} knockdowns");
        }

        protected override MinigameTelemetry SampleTelemetry()
        {
            float fastest = 0f;
            foreach (BumperActor player in players)
            {
                fastest = Mathf.Max(fastest, player.Velocity.magnitude / player.MaxSpeed);
            }

            return new MinigameTelemetry
            {
                PlayerMotion = fastest,
                ChefMotion = chef == null ? 0f : chef.Velocity.magnitude / chef.MaxSpeed,
                Objective = hits / (float)HitsToWin,
            };
        }

        protected override IParticipantInput CreateCpuBrain(Participant participant)
        {
            return new BumperPartnerBrain(Snapshot, participant.Slot, participant.Skill);
        }
    }
}
