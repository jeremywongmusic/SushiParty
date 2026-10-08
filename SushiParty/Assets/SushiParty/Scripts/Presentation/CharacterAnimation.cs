using UnityEngine;

namespace SushiParty.Presentation
{
    public sealed class CharacterAnimation
    {
        public const string ControllerResource = "Animation/Octopus";
        public const string SpeedParameter = "Speed";
        public const string GroundedParameter = "Grounded";
        public const string JumpParameter = "Jump";
        public const string LandParameter = "Land";
        public const string PoundParameter = "Pound";
        public const string CheerParameter = "Cheer";
        public const string RollParameter = "Roll";
        public const string DelightParameter = "Delight";
        public const string DismayParameter = "Dismay";
        public const string DizzyParameter = "Dizzy";
        public const string StunnedParameter = "Stunned";
        private static readonly int SpeedId = Animator.StringToHash(SpeedParameter);
        private static readonly int GroundedId = Animator.StringToHash(GroundedParameter);
        private static readonly int JumpId = Animator.StringToHash(JumpParameter);
        private static readonly int LandId = Animator.StringToHash(LandParameter);
        private static readonly int PoundId = Animator.StringToHash(PoundParameter);
        private static readonly int CheerId = Animator.StringToHash(CheerParameter);
        private static readonly int RollId = Animator.StringToHash(RollParameter);
        private static readonly int DelightId = Animator.StringToHash(DelightParameter);
        private static readonly int DismayId = Animator.StringToHash(DismayParameter);
        private static readonly int DizzyId = Animator.StringToHash(DizzyParameter);
        private static readonly int StunnedId = Animator.StringToHash(StunnedParameter);
        private static readonly CharacterAnimation Silent = new CharacterAnimation(null);
        private static RuntimeAnimatorController controller;
        private static bool searched;
        private readonly Animator animator;

        private CharacterAnimation(Animator animator)
        {
            this.animator = animator;
        }

        public bool IsAnimating => animator != null;
        private bool Ready => animator != null && animator.isInitialized;

        public static CharacterAnimation Attach(Transform octopusRoot)
        {
            if (octopusRoot == null)
            {
                return Silent;
            }

            Animator animator = octopusRoot.GetComponent<Animator>();
            RuntimeAnimatorController asset = animator != null && animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController
                : LoadController();

            if (asset == null)
            {
                return Silent;
            }

            if (animator == null)
            {
                animator = octopusRoot.gameObject.AddComponent<Animator>();
            }

            if (animator.runtimeAnimatorController != asset)
            {
                animator.runtimeAnimatorController = asset;
            }

            animator.applyRootMotion = false;

            if (octopusRoot.GetComponent<CharacterAudioRelay>() == null)
            {
                octopusRoot.gameObject.AddComponent<CharacterAudioRelay>();
            }

            return new CharacterAnimation(animator);
        }

        public void SetSpeed(float normalised)
        {
            if (!Ready)
            {
                return;
            }

            // A zero-length frame gives NaN, and a NaN in an Animator parameter never comes back out.
            animator.SetFloat(SpeedId, normalised > 0f ? Mathf.Min(normalised, 1f) : 0f);
        }

        public void SetGrounded(bool grounded)
        {
            if (!Ready)
            {
                return;
            }

            animator.SetBool(GroundedId, grounded);
        }

        public void Jump()
        {
            if (!Ready)
            {
                return;
            }

            animator.SetTrigger(JumpId);
        }

        public void Land()
        {
            if (!Ready)
            {
                return;
            }

            animator.SetTrigger(LandId);
        }

        public void Pound()
        {
            if (!Ready)
            {
                return;
            }

            animator.SetTrigger(PoundId);
        }

        public void SetStunned(bool stunned)
        {
            if (!Ready)
            {
                return;
            }

            animator.SetBool(StunnedId, stunned);
        }

        public void Cheer()
        {
            if (!Ready)
            {
                return;
            }

            animator.SetBool(StunnedId, false);
            animator.SetTrigger(CheerId);
        }

        public void Roll()
        {
            if (!Ready)
            {
                return;
            }

            animator.SetTrigger(RollId);
        }

        public void Delight()
        {
            if (!Ready)
            {
                return;
            }

            animator.SetTrigger(DelightId);
        }

        public void Dismay()
        {
            if (!Ready)
            {
                return;
            }

            animator.SetTrigger(DismayId);
        }

        public void Dizzy()
        {
            if (!Ready)
            {
                return;
            }

            animator.SetTrigger(DizzyId);
        }

        private static RuntimeAnimatorController LoadController()
        {
            if (searched)
            {
                return controller;
            }

            searched = true;
            controller = Resources.Load<RuntimeAnimatorController>(ControllerResource);

            if (controller == null)
            {
                Debug.Log(
                    $"[SushiParty] No animation controller at Resources/{ControllerResource}. "
                    + "The cast will play unanimated.");
            }

            return controller;
        }
    }
}
