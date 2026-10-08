using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

namespace SushiParty.Board
{
    public enum BoardShot
    {
        Map,
        Turn,
        Fork,
        Reaction,
    }

    public sealed class BoardCameraRig
    {
        private const float TravelBlend = 0.45f;

        private const CinemachineBlendDefinition.Styles TravelShape =
            CinemachineBlendDefinition.Styles.EaseInOut;

        private const float ReframeBlend = 0.28f;

        private const CinemachineBlendDefinition.Styles ReframeShape =
            CinemachineBlendDefinition.Styles.EaseIn;

        private const int MapPriority = 10;
        private const int ActivePriority = 20;
        private const int StandbyPriority = 0;
        private const float TurnShoulder = 1.0f;
        private const float TurnHeight = 3.6f;
        private const float TurnBehind = -4.6f;
        private const float TurnAimHeight = 1.7f;
        private const float ForkShoulder = 0f;
        private const float ForkHeight = 7.6f;
        private const float ForkBehind = -8.2f;
        private const float ForkAimHeight = 1.2f;
        private const float ReactionShoulder = -1.0f;
        private const float ReactionHeight = 1.35f;
        private const float ReactionAhead = 2.15f;
        private const float ReactionAimHeight = 0.95f;
        private const float TrailDamping = 0.45f;
        private const float HopDamping = 0.9f;
        private const float SwingDamping = 0.55f;
        private const float AimDampingPan = 0.35f;
        private const float AimDampingTilt = 0.55f;
        private const float DeadZoneWidth = 0.12f;
        private const float DeadZoneHeight = 0.24f;
        private const float ReactionDamping = 0.15f;
        private const float FacingEpsilon = 0.0001f;
        private const string RigName = "Board Camera Rig";
        private const string BlendsName = "Board Camera Blends";
        private const string MapName = "CM Board Map";
        private const string TurnName = "CM Board Turn";
        private const string ForkName = "CM Board Fork";
        private const string ReactionName = "CM Board Reaction";
        private static readonly BoardCameraRig Blind = new BoardCameraRig();
        private readonly Shot[] shots;
        private BoardShot current = BoardShot.Map;
        private Transform focus;

        private BoardCameraRig()
        {
        }

        private BoardCameraRig(Camera boardCamera)
        {
            EnsureBrain(boardCamera);

            Transform rig = new GameObject(RigName).transform;

            shots = new Shot[(int)BoardShot.Reaction + 1];

            shots[(int)BoardShot.Map] = new Shot(
                BuildMap(rig, boardCamera), Vector3.zero, 0f);

            shots[(int)BoardShot.Turn] = BuildComposed(
                rig, boardCamera, TurnName,
                new Vector3(TurnShoulder, TurnHeight, TurnBehind), TurnAimHeight);

            shots[(int)BoardShot.Fork] = BuildComposed(
                rig, boardCamera, ForkName,
                new Vector3(ForkShoulder, ForkHeight, ForkBehind), ForkAimHeight);

            shots[(int)BoardShot.Reaction] = BuildReaction(
                rig, boardCamera,
                new Vector3(ReactionShoulder, ReactionHeight, ReactionAhead), ReactionAimHeight);
        }

        public static BoardCameraRig Create(Camera boardCamera)
        {
            if (boardCamera == null)
            {
                Debug.LogWarning(
                    "[SushiParty] No board camera was handed to BoardCameraRig — the board will play " +
                    "with a fixed camera and no turn shots.");

                return Blind;
            }

            return new BoardCameraRig(boardCamera);
        }

        public BoardShot Current => current;

        public void Cut(BoardShot shot, Transform token = null)
        {
            if (shots == null)
            {
                return;
            }

            if (shot != BoardShot.Map && token == null)
            {
                shot = BoardShot.Map;
            }

            if (shot == current && (shot == BoardShot.Map || focus == token))
            {
                return;
            }

            BoardShot previous = current;
            current = shot;

            if (shot == BoardShot.Map)
            {
                Drop(previous);
                focus = null;

                Live(shots[(int)BoardShot.Map].Camera, MapPriority);
                return;
            }

            focus = token;

            Shot next = shots[(int)shot];
            next.Camera.Target.TrackingTarget = token;

            next.Camera.Target.CustomLookAtTarget = false;

            if (previous != shot)
            {
                Aim(next);
                Drop(previous);
            }

            Live(next.Camera, ActivePriority);
        }

        public void Release()
        {
            Cut(BoardShot.Map);
        }

        private static void EnsureBrain(Camera boardCamera)
        {
            if (!boardCamera.TryGetComponent(out CinemachineBrain brain))
            {
                brain = boardCamera.gameObject.AddComponent<CinemachineBrain>();
            }

            brain.DefaultBlend = new CinemachineBlendDefinition(TravelShape, TravelBlend);
            brain.CustomBlends = BuildBlends();
        }

        private static CinemachineBlenderSettings BuildBlends()
        {
            CinemachineBlenderSettings blends = ScriptableObject.CreateInstance<CinemachineBlenderSettings>();
            blends.name = BlendsName;
            blends.hideFlags = HideFlags.HideAndDontSave;

            string[] onTheToken = { TurnName, ForkName, ReactionName };
            CinemachineBlendDefinition reframe = new CinemachineBlendDefinition(ReframeShape, ReframeBlend);

            blends.CustomBlends =
                new CinemachineBlenderSettings.CustomBlend[onTheToken.Length * (onTheToken.Length - 1)];

            int next = 0;
            for (int from = 0; from < onTheToken.Length; from++)
            {
                for (int to = 0; to < onTheToken.Length; to++)
                {
                    if (from == to)
                    {
                        continue;
                    }

                    blends.CustomBlends[next++] = new CinemachineBlenderSettings.CustomBlend
                    {
                        From = onTheToken[from],
                        To = onTheToken[to],
                        Blend = reframe,
                    };
                }
            }

            return blends;
        }

        private static CinemachineCamera BuildMap(Transform rig, Camera boardCamera)
        {
            CinemachineCamera camera = NewCamera(rig, MapName);
            Transform authored = boardCamera.transform;

            camera.transform.SetPositionAndRotation(authored.position, authored.rotation);

            // Cinemachine pushes its own lens onto the camera, so copy the authored one or
            // the board quietly widens from 60 degrees to Cinemachine's default 40.
            camera.Lens = LensSettings.FromCamera(boardCamera);

            Live(camera, MapPriority);
            return camera;
        }

        private static Shot BuildComposed(
            Transform rig, Camera boardCamera, string name, Vector3 offset, float aimHeight)
        {
            CinemachineCamera camera = NewCamera(rig, name);

            camera.Lens = LensSettings.FromCamera(boardCamera);

            CinemachineFollow follow = camera.gameObject.AddComponent<CinemachineFollow>();

            follow.TrackerSettings.BindingMode = BindingMode.LockToTargetWithWorldUp;
            follow.TrackerSettings.PositionDamping = new Vector3(TrailDamping, HopDamping, TrailDamping);
            follow.TrackerSettings.RotationDamping = new Vector3(0f, SwingDamping, 0f);
            follow.FollowOffset = offset;

            CinemachineRotationComposer composer =
                camera.gameObject.AddComponent<CinemachineRotationComposer>();

            composer.TargetOffset = new Vector3(0f, aimHeight, 0f);
            composer.Damping = new Vector2(AimDampingPan, AimDampingTilt);
            composer.Composition.DeadZone.Enabled = true;
            composer.Composition.DeadZone.Size = new Vector2(DeadZoneWidth, DeadZoneHeight);

            composer.Composition.ScreenPosition = Vector2.zero;

            composer.CenterOnActivate = true;

            Live(camera, StandbyPriority);
            return new Shot(camera, offset, aimHeight);
        }

        private static Shot BuildReaction(
            Transform rig, Camera boardCamera, Vector3 offset, float aimHeight)
        {
            CinemachineCamera camera = NewCamera(rig, ReactionName);

            camera.Lens = LensSettings.FromCamera(boardCamera);

            CinemachineFollow follow = camera.gameObject.AddComponent<CinemachineFollow>();

            follow.TrackerSettings.BindingMode = BindingMode.LockToTargetWithWorldUp;
            follow.TrackerSettings.PositionDamping = Vector3.one * ReactionDamping;
            follow.TrackerSettings.RotationDamping = new Vector3(0f, ReactionDamping, 0f);
            follow.FollowOffset = offset;

            CinemachineHardLookAt lookAt = camera.gameObject.AddComponent<CinemachineHardLookAt>();
            lookAt.LookAtOffset = new Vector3(0f, aimHeight, 0f);

            Live(camera, StandbyPriority);
            return new Shot(camera, offset, aimHeight);
        }

        private static CinemachineCamera NewCamera(Transform rig, string name)
        {
            GameObject holder = new GameObject(name);
            holder.transform.SetParent(rig, false);

            return holder.AddComponent<CinemachineCamera>();
        }

        private static void Live(CinemachineCamera camera, int priority)
        {
            camera.Priority = priority;
            camera.Prioritize();
        }

        private void Drop(BoardShot shot)
        {
            if (shot == BoardShot.Map)
            {
                return;
            }

            CinemachineCamera camera = shots[(int)shot].Camera;

            camera.Target.TrackingTarget = null;
            Live(camera, StandbyPriority);
        }

        private static void Aim(in Shot shot)
        {
            Transform token = shot.Camera.Target.TrackingTarget;
            if (token == null)
            {
                return;
            }

            Vector3 forward = token.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > FacingEpsilon ? forward.normalized : Vector3.forward;

            Vector3 right = Vector3.Cross(Vector3.up, forward);

            Vector3 stand = token.position
                + right * shot.Offset.x
                + Vector3.up * shot.Offset.y
                + forward * shot.Offset.z;

            Vector3 aim = token.position + Vector3.up * shot.AimHeight;

            shot.Camera.ForceCameraPosition(stand, Quaternion.LookRotation(aim - stand, Vector3.up));
        }

        private readonly struct Shot
        {
            public readonly CinemachineCamera Camera;
            public readonly Vector3 Offset;
            public readonly float AimHeight;

            public Shot(CinemachineCamera camera, Vector3 offset, float aimHeight)
            {
                Camera = camera;
                Offset = offset;
                AimHeight = aimHeight;
            }
        }
    }
}
