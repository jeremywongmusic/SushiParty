using SushiParty.Audio;
using SushiParty.Presentation;
using UnityEngine;

namespace SushiParty.Board
{
    public sealed class BoardDiceBlock
    {
        private const float BlockSize = 0.80f;
        private const float BevelRadius = 0.10f;
        private const float PipDiameter = 0.15f;
        private const float PipSpacing = 0.20f;
        private const float PipInset = 0.035f;
        private const float PipFlatten = 0.55f;
        private const int FaceCount = 6;
        private const float HoverHeight = 1.95f;
        private const float BobHeight = 0.085f;
        private const float BobCycles = 0.55f;
        private const float FollowRate = 11f;
        private const float IdleSpinSpeed = 48f;
        private const float IdleTiltDegrees = 17f;
        private const float CycleSpinSpeed = 620f;
        private const float CycleInterval = 0.06f;
        private const float CycleJitter = 0.45f;
        private const float CycleLeanDegrees = 26f;
        private const float LaunchKickSpeed = 6f;
        private const float LaunchPunch = 0.22f;
        private const float LaunchPunchDuration = 0.22f;
        private const float KickSpring = 90f;
        private const float KickDamping = 9f;
        private const float SettleDropSpeed = 2.1f;
        private const float SettleDuration = 0.26f;
        private const float SettleOvershoot = 1.6f;
        private const float SettleSquash = 0.16f;
        private const float TrackRate = 6f;
        private const float AppearDuration = 0.24f;
        private const float AppearBounce = 1.7f;
        private const float PopDuration = 0.20f;
        private const float PopSwell = 1.3f;
        private const float PopSwellShare = 0.35f;
        private const string BlockName = "DiceBlock";
        private static readonly Color RiceBlock = Palette.RiceWhite;
        private static readonly Color PipNori = new Color(0.07f, 0.06f, 0.10f);

        private static readonly Vector3[] FaceNormals =
        {
            Vector3.forward,
            Vector3.right,
            Vector3.up,
            Vector3.down,
            Vector3.left,
            Vector3.back,
        };

        private static readonly int[][] PipLayouts =
        {
            new[] { 0, 0 },
            new[] { -1, -1, 1, 1 },
            new[] { -1, -1, 0, 0, 1, 1 },
            new[] { -1, -1, -1, 1, 1, -1, 1, 1 },
            new[] { -1, -1, -1, 1, 0, 0, 1, -1, 1, 1 },
            new[] { -1, -1, -1, 0, -1, 1, 1, -1, 1, 0, 1, 1 },
        };

        private static readonly Quaternion IdleTilt =
            Quaternion.Euler(IdleTiltDegrees, 0f, IdleTiltDegrees * 0.6f);

        private enum Beat
        {
            Gone,
            Waiting,
            Cycling,
            Settled,
            Popping,
        }

        private readonly int[] bag = { 1, 2, 3, 4, 5, 6 };

        private readonly Transform root;
        private readonly Transform viewer;
        private Transform owner;
        private Beat beat = Beat.Gone;
        private float clock;
        private float appear;
        private float bob;
        private float turn;
        private Vector3 home;
        private float kick;
        private float kickVelocity;
        private Quaternion settleFrom = Quaternion.identity;
        private int showingFace;
        private int drawn = FaceCount;
        private float nextFlip;
        private Vector3 leanAxis = Vector3.up;

        private BoardDiceBlock(Transform parent)
        {
            root = new GameObject(BlockName).transform;
            root.SetParent(parent, false);

            BuildBody(root);

            for (int face = 1; face <= FaceCount; face++)
            {
                BuildFace(root, face);
            }

            Camera live = Camera.main;
            viewer = live != null ? live.transform : null;

            root.gameObject.SetActive(false);
        }

        public static BoardDiceBlock Create(Transform parent)
        {
            return new BoardDiceBlock(parent);
        }

        public bool Visible => beat != Beat.Gone;
        public bool Cycling => beat == Beat.Cycling;
        public int ShowingFace => showingFace;

        public void Present(Transform token)
        {
            if (token == null)
            {
                Dismiss();
                return;
            }

            if (beat == Beat.Waiting && owner == token)
            {
                return;
            }

            owner = token;
            home = token.position;

            beat = Beat.Waiting;
            clock = 0f;
            appear = 0f;
            turn = 0f;
            kick = 0f;
            kickVelocity = 0f;
            showingFace = 0;

            root.gameObject.SetActive(true);
            root.localScale = Vector3.zero;
            root.SetPositionAndRotation(home + new Vector3(0f, HoverHeight, 0f), IdleTilt);
        }

        public void Launch()
        {
            if (beat != Beat.Waiting && beat != Beat.Settled)
            {
                return;
            }

            beat = Beat.Cycling;
            clock = 0f;
            turn = 0f;
            nextFlip = 0f;
            kickVelocity = LaunchKickSpeed;

            drawn = FaceCount;
            showingFace = 0;

            Flip();

            GameAudio.PlayAt(Sfx.BoardDieRoll, root.position);
        }

        public int Stop()
        {
            int face = showingFace != 0 ? showingFace : NextFace();

            if (beat == Beat.Cycling || beat == Beat.Waiting)
            {
                Land(face);
            }

            return face;
        }

        public void Spin(float deltaTime)
        {
            if (beat == Beat.Gone)
            {
                return;
            }

            // A comparison rather than a clamp, because NaN fails it.
            float step = deltaTime > 0f ? deltaTime : 0f;

            clock += step;
            appear += step;
            bob += step;

            switch (beat)
            {
                case Beat.Waiting:
                    turn += IdleSpinSpeed * step;
                    root.rotation = Quaternion.AngleAxis(turn, Vector3.up) * IdleTilt;
                    break;

                case Beat.Cycling:
                    if (clock >= nextFlip)
                    {
                        Flip();
                    }

                    turn += CycleSpinSpeed * step;
                    root.rotation = CyclePose();
                    break;

                case Beat.Settled:
                    Aim(step);
                    break;
            }

            Float(step);
            Resize();

            if (beat == Beat.Popping && clock >= PopDuration)
            {
                beat = Beat.Gone;
                root.gameObject.SetActive(false);
            }
        }

        public void Settle(int face)
        {
            if (beat == Beat.Gone || beat == Beat.Popping)
            {
                return;
            }

            Land(face);
        }

        public void Dismiss()
        {
            if (beat == Beat.Gone || beat == Beat.Popping)
            {
                return;
            }

            beat = Beat.Popping;
            clock = 0f;

            owner = null;
        }

        private void Land(int face)
        {
            showingFace = face < 1 ? 1 : (face > FaceCount ? FaceCount : face);

            beat = Beat.Settled;
            clock = 0f;
            settleFrom = root.rotation;

            kickVelocity -= SettleDropSpeed;

            GameAudio.PlayAt(Sfx.BoardDieLand, root.position, Sfx.StepParameter, showingFace);
        }

        private void Flip()
        {
            showingFace = NextFace();
            leanAxis = Random.onUnitSphere;
            nextFlip = clock + (CycleInterval * (1f + Random.Range(-CycleJitter, CycleJitter)));
        }

        private int NextFace()
        {
            if (drawn >= FaceCount)
            {
                Shuffle();
                drawn = 0;
            }

            return bag[drawn++];
        }

        private void Shuffle()
        {
            for (int i = FaceCount - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);

                int held = bag[i];
                bag[i] = bag[j];
                bag[j] = held;
            }

            if (bag[0] == showingFace)
            {
                int other = Random.Range(1, FaceCount);

                bag[0] = bag[other];
                bag[other] = showingFace;
            }
        }

        private void Float(float step)
        {
            if (owner != null)
            {
                home = Vector3.Lerp(home, owner.position, Catch(FollowRate, step));
            }

            kickVelocity += ((-KickSpring * kick) - (KickDamping * kickVelocity)) * step;
            kick += kickVelocity * step;

            float lift = HoverHeight + kick + (Mathf.Sin(bob * BobCycles * Mathf.PI * 2f) * BobHeight);
            root.position = home + new Vector3(0f, lift, 0f);
        }

        private Quaternion CyclePose()
        {
            return Quaternion.AngleAxis(turn, ViewDirection())
                   * Quaternion.AngleAxis(CycleLeanDegrees, leanAxis)
                   * FacingFor(showingFace);
        }

        private void Aim(float step)
        {
            Quaternion facing = FacingFor(showingFace);

            if (clock < SettleDuration)
            {
                root.rotation = Quaternion.SlerpUnclamped(
                    settleFrom, facing, Overshoot(clock / SettleDuration, SettleOvershoot));

                return;
            }

            root.rotation = Quaternion.Slerp(root.rotation, facing, Catch(TrackRate, step));
        }

        private Quaternion FacingFor(int face)
        {
            Vector3 toViewer = ViewDirection();

            Vector3 reference = Mathf.Abs(toViewer.y) > 0.98f ? Vector3.forward : Vector3.up;

            return Quaternion.LookRotation(toViewer, reference)
                   * Quaternion.FromToRotation(FaceNormals[face - 1], Vector3.forward);
        }

        private Vector3 ViewDirection()
        {
            Vector3 toViewer;

            if (viewer != null)
            {
                toViewer = viewer.position - root.position;
            }
            else if (owner != null)
            {
                toViewer = owner.forward;
            }
            else
            {
                toViewer = Vector3.forward;
            }

            return toViewer.sqrMagnitude < 0.0001f ? Vector3.forward : toViewer.normalized;
        }

        private void Resize()
        {
            float size = appear < AppearDuration ? Overshoot(appear / AppearDuration, AppearBounce) : 1f;

            if (beat == Beat.Cycling && clock < LaunchPunchDuration)
            {
                float t = clock / LaunchPunchDuration;

                size -= LaunchPunch * Mathf.Sin(t * Mathf.PI * 2f) * (1f - t);
            }
            else if (beat == Beat.Settled && clock < SettleDuration)
            {
                float t = clock / SettleDuration;

                size += SettleSquash * Mathf.Sin(Mathf.PI * t) * (1f - t);
            }
            else if (beat == Beat.Popping)
            {
                size = PopSize(clock / PopDuration);
            }

            root.localScale = new Vector3(size, size, size);
        }

        private static float PopSize(float t)
        {
            if (t < PopSwellShare)
            {
                return Mathf.Lerp(1f, PopSwell, t / PopSwellShare);
            }

            float fall = (t - PopSwellShare) / (1f - PopSwellShare);
            return Mathf.Lerp(PopSwell, 0f, fall * fall);
        }

        private static float Overshoot(float t, float amount)
        {
            float back = t - 1f;
            return 1f + ((amount + 1f) * back * back * back) + (amount * back * back);
        }

        private static float Catch(float rate, float step)
        {
            return 1f - Mathf.Exp(-rate * step);
        }

        private static void BuildBody(Transform parent)
        {
            Transform body = new GameObject("Block").transform;
            body.SetParent(parent, false);

            float half = BlockSize * 0.5f;

            float inner = half - BevelRadius;

            float flat = BlockSize - (BevelRadius * 2f);
            float bevel = BevelRadius * 2f;

            Shapes.Cube(body, Vector3.zero, new Vector3(BlockSize, flat, flat), RiceBlock, "SlabX");
            Shapes.Cube(body, Vector3.zero, new Vector3(flat, BlockSize, flat), RiceBlock, "SlabY");
            Shapes.Cube(body, Vector3.zero, new Vector3(flat, flat, BlockSize), RiceBlock, "SlabZ");

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? -inner : inner,
                    (i & 2) == 0 ? -inner : inner,
                    (i & 4) == 0 ? -inner : inner);

                Shapes.Sphere(body, corner, bevel, RiceBlock, $"Corner{i}");
            }

            for (int axis = 0; axis < 3; axis++)
            {
                for (int i = 0; i < 4; i++)
                {
                    float across = (i & 1) == 0 ? -inner : inner;
                    float along = (i & 2) == 0 ? -inner : inner;

                    // A Unity cylinder is two units tall on its own Y, so the scale is the half-length.
                    Vector3 where = axis == 0
                        ? new Vector3(0f, across, along)
                        : (axis == 1 ? new Vector3(across, 0f, along) : new Vector3(across, along, 0f));

                    Quaternion lie = axis == 0
                        ? Quaternion.Euler(0f, 0f, 90f)
                        : (axis == 1 ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f));

                    Transform edge = Shapes.Cylinder(
                        body, where, new Vector3(bevel, inner, bevel), RiceBlock, $"Edge{axis}{i}");

                    edge.localRotation = lie;
                }
            }
        }

        private static void BuildFace(Transform parent, int face)
        {
            Vector3 normal = FaceNormals[face - 1];

            Quaternion frame = Quaternion.LookRotation(
                normal, Mathf.Abs(normal.y) > 0.5f ? Vector3.forward : Vector3.up);

            Vector3 across = frame * Vector3.right;
            Vector3 up = frame * Vector3.up;
            Vector3 centre = normal * ((BlockSize * 0.5f) - PipInset);

            int[] layout = PipLayouts[face - 1];

            for (int i = 0; i < layout.Length; i += 2)
            {
                Transform pip = Shapes.Create(
                    PrimitiveType.Sphere,
                    parent,
                    centre + (across * (layout[i] * PipSpacing)) + (up * (layout[i + 1] * PipSpacing)),
                    new Vector3(PipDiameter, PipDiameter, PipDiameter * PipFlatten),
                    PipNori,
                    $"Pip{face}_{i / 2}");

                pip.localRotation = frame;
            }
        }
    }
}
