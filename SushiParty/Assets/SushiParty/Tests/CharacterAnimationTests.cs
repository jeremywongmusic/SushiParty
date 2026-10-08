using NUnit.Framework;
using SushiParty.Presentation;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace SushiParty.Tests
{
    public sealed class CharacterAnimationTests
    {
        private readonly List<GameObject> characters = new List<GameObject>();
        private readonly List<AnimatorController> controllers = new List<AnimatorController>();

        [TearDown]
        public void ClearTheStage()
        {
            foreach (GameObject character in characters)
            {
                if (character != null)
                {
                    Object.DestroyImmediate(character);
                }
            }

            foreach (AnimatorController controller in controllers)
            {
                if (controller != null)
                {
                    Object.DestroyImmediate(controller);
                }
            }

            characters.Clear();
            controllers.Clear();
        }

        [Test]
        public void A_missing_view_still_hands_back_a_usable_handle()
        {
            CharacterAnimation animation = CharacterAnimation.Attach(null);

            Assert.That(animation, Is.Not.Null, "a null handle would put a guard at every call site");
            Assert.That(animation.IsAnimating, Is.False);
            Assert.DoesNotThrow(() => DriveEverything(animation));
        }

        [Test]
        public void Attach_only_touches_a_character_when_there_is_a_controller_to_drive_it()
        {
            bool generated = Resources.Load<RuntimeAnimatorController>(CharacterAnimation.ControllerResource) != null;

            Transform character = NewCharacter("Plain");
            CharacterAnimation animation = CharacterAnimation.Attach(character);

            Assert.That(animation, Is.Not.Null);
            Assert.That(animation.IsAnimating, Is.EqualTo(generated), "the handle must own up to what it found");
            Assert.That(
                character.GetComponent<Animator>() != null,
                Is.EqualTo(generated),
                "no controller means no Animator left behind on the character");
            Assert.DoesNotThrow(() => DriveEverything(animation));
        }

        [Test]
        public void Every_verb_is_safe_on_a_live_handle()
        {
            CharacterAnimation animation = CharacterAnimation.Attach(NewLiveCharacter("Driven"));

            Assert.That(animation.IsAnimating, Is.True, "a character with a controller is animated");
            Assert.DoesNotThrow(() => DriveEverything(animation));
        }

        [Test]
        public void Attach_keeps_the_animator_and_the_controller_a_character_already_has()
        {
            Transform character = NewLiveCharacter("Twice");
            RuntimeAnimatorController assigned = character.GetComponent<Animator>().runtimeAnimatorController;

            CharacterAnimation.Attach(character);
            CharacterAnimation.Attach(character);

            Assert.That(
                character.GetComponents<Animator>().Length,
                Is.EqualTo(1),
                "a second Attach must not stack a second Animator on the character");
            Assert.That(
                character.GetComponent<Animator>().runtimeAnimatorController,
                Is.SameAs(assigned),
                "a controller somebody already chose must survive being attached to");
        }

        [Test]
        public void Root_motion_is_off_so_the_animator_cannot_move_the_character()
        {
            Transform character = NewLiveCharacter("Rooted");
            Animator animator = character.GetComponent<Animator>();
            animator.applyRootMotion = true;

            CharacterAnimation.Attach(character);

            Assert.That(
                animator.applyRootMotion,
                Is.False,
                "root motion writes the root transform, which belongs to the minigame");
        }

        [Test]
        public void A_destroyed_character_degrades_instead_of_throwing()
        {
            Transform character = NewLiveCharacter("Doomed");
            CharacterAnimation animation = CharacterAnimation.Attach(character);

            Object.DestroyImmediate(character.gameObject);

            Assert.That(animation.IsAnimating, Is.False, "a destroyed Animator animates nothing");
            Assert.DoesNotThrow(() => DriveEverything(animation));
        }

        [Test]
        public void The_generated_controller_carries_every_parameter_the_runtime_writes()
        {
            AnimatorController generated =
                Resources.Load<RuntimeAnimatorController>(CharacterAnimation.ControllerResource) as AnimatorController;

            if (generated == null)
            {
                Assert.Ignore("Nothing generated in this clone.");
            }

            AssertParameter(generated, CharacterAnimation.SpeedParameter, AnimatorControllerParameterType.Float);
            AssertParameter(generated, CharacterAnimation.GroundedParameter, AnimatorControllerParameterType.Bool);
            AssertParameter(generated, CharacterAnimation.StunnedParameter, AnimatorControllerParameterType.Bool);
            AssertParameter(generated, CharacterAnimation.JumpParameter, AnimatorControllerParameterType.Trigger);
            AssertParameter(generated, CharacterAnimation.LandParameter, AnimatorControllerParameterType.Trigger);
            AssertParameter(generated, CharacterAnimation.PoundParameter, AnimatorControllerParameterType.Trigger);
            AssertParameter(generated, CharacterAnimation.CheerParameter, AnimatorControllerParameterType.Trigger);
        }

        private static void DriveEverything(CharacterAnimation animation)
        {
            animation.SetSpeed(0f);
            animation.SetSpeed(1f);
            animation.SetSpeed(-3f);
            animation.SetSpeed(12f);
            animation.SetSpeed(float.NaN);
            animation.SetSpeed(float.PositiveInfinity);

            animation.SetGrounded(false);
            animation.Jump();
            animation.Pound();
            animation.SetGrounded(true);
            animation.Land();

            animation.SetStunned(true);
            animation.SetStunned(false);
            animation.Cheer();
        }

        private static void AssertParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name != name)
                {
                    continue;
                }

                Assert.That(parameter.type, Is.EqualTo(type), $"'{name}' is the wrong kind of parameter");
                return;
            }

            Assert.Fail($"the generated controller has no '{name}' parameter");
        }

        private Transform NewCharacter(string name)
        {
            GameObject character = new GameObject(name);
            characters.Add(character);
            return character.transform;
        }

        private Transform NewLiveCharacter(string name)
        {
            Transform character = NewCharacter(name);
            character.gameObject.AddComponent<Animator>().runtimeAnimatorController = StandInController();
            return character;
        }

        private AnimatorController StandInController()
        {
            AnimatorController controller = new AnimatorController { name = "StandInOctopus" };
            controllers.Add(controller);

            controller.AddParameter(CharacterAnimation.SpeedParameter, AnimatorControllerParameterType.Float);
            controller.AddParameter(CharacterAnimation.GroundedParameter, AnimatorControllerParameterType.Bool);
            controller.AddParameter(CharacterAnimation.StunnedParameter, AnimatorControllerParameterType.Bool);
            controller.AddParameter(CharacterAnimation.JumpParameter, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(CharacterAnimation.LandParameter, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(CharacterAnimation.PoundParameter, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(CharacterAnimation.CheerParameter, AnimatorControllerParameterType.Trigger);

            return controller;
        }
    }
}
