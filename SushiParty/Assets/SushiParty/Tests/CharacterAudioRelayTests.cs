using NUnit.Framework;
using SushiParty.Audio;
using SushiParty.Presentation;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;

namespace SushiParty.Tests
{
    public sealed class CharacterAudioRelayTests
    {
        private readonly List<GameObject> characters = new List<GameObject>();
        private readonly List<AnimatorController> controllers = new List<AnimatorController>();

        [SetUp]
        public void ForgetPreviousComplaints()
        {
            CharacterAudioRelay.ForgetReportedNames();
        }

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
        public void Every_entry_answers_to_its_member_name()
        {
            foreach (FieldInfo field in CatalogueFields())
            {
                AudioEvent declared = (AudioEvent)field.GetValue(null);

                Assert.That(
                    Sfx.TryFind(field.Name, out AudioEvent found),
                    Is.True,
                    $"'{field.Name}' is in the catalogue but cannot be found by name");
                Assert.That(
                    found.Path,
                    Is.EqualTo(declared.Path),
                    $"'{field.Name}' resolved to the wrong sound");
            }
        }

        [Test]
        public void Every_entry_answers_to_its_full_studio_path()
        {
            foreach (FieldInfo field in CatalogueFields())
            {
                AudioEvent declared = (AudioEvent)field.GetValue(null);

                Assert.That(
                    Sfx.TryFind(declared.Path, out AudioEvent found),
                    Is.True,
                    $"the path '{declared.Path}' cannot be found");
                Assert.That(found.Path, Is.EqualTo(declared.Path));
            }
        }

        [Test]
        public void The_catalogue_is_worth_reflecting_over()
        {
            Assert.That(
                CatalogueFields().Count,
                Is.GreaterThan(80),
                "Sfx should have the best part of a hundred entries — has the shape changed?");
        }

        [Test]
        public void Case_does_not_matter()
        {
            Assert.That(Sfx.TryFind("characterfootstep", out AudioEvent lower), Is.True);
            Assert.That(lower.Path, Is.EqualTo(Sfx.CharacterFootstep.Path));

            Assert.That(Sfx.TryFind("CHARACTERFOOTSTEP", out AudioEvent upper), Is.True);
            Assert.That(upper.Path, Is.EqualTo(Sfx.CharacterFootstep.Path));

            Assert.That(Sfx.TryFind("EVENT:/sfx/Character/FOOTSTEP", out AudioEvent path), Is.True);
            Assert.That(path.Path, Is.EqualTo(Sfx.CharacterFootstep.Path));
        }

        [Test]
        public void Whitespace_round_the_edges_does_not_matter()
        {
            Assert.That(Sfx.TryFind("  CharacterFootstep  ", out AudioEvent padded), Is.True);
            Assert.That(padded.Path, Is.EqualTo(Sfx.CharacterFootstep.Path));

            Assert.That(Sfx.TryFind("\tCharacterCheer\n", out AudioEvent tabbed), Is.True);
            Assert.That(tabbed.Path, Is.EqualTo(Sfx.CharacterCheer.Path));

            Assert.That(Sfx.TryFind(" event:/SFX/Character/Jump ", out AudioEvent path), Is.True);
            Assert.That(path.Path, Is.EqualTo(Sfx.CharacterJump.Path));
        }

        [Test]
        public void Nonsense_resolves_to_nothing()
        {
            string[] rubbish =
            {
                null,
                string.Empty,
                "   ",
                "Footstep",                          // the tail of a path, not a name
                "Character",                         // a group, not an event
                "CharacterFootsteps",                // the plural somebody will type
                "CharacterFootstep ish",
                "Sfx.CharacterFootstep",             // pasted with the class in front
                "event:/SFX/Character/Footsteps",
                "event:/SFX/Character",
                "event:/",
                "StepParameter",                     // a parameter name, not an event
                "AllNames",
            };

            foreach (string name in rubbish)
            {
                Assert.That(
                    Sfx.TryFind(name, out AudioEvent found),
                    Is.False,
                    $"'{name ?? "null"}' should not resolve to anything");
                Assert.That(found.IsValid, Is.False, "a miss must not hand back a playable event");
            }
        }

        [Test]
        public void Find_hands_back_something_harmless_when_it_knows_nothing()
        {
            AudioEvent missing = Sfx.Find("NoSuchSoundAnywhere");

            Assert.That(missing.IsValid, Is.False);
            Assert.That(
                Sfx.Find("CharacterCheer").Path,
                Is.EqualTo(Sfx.CharacterCheer.Path),
                "Find and TryFind must agree about the same name");
        }

        [Test]
        public void AllNames_is_the_whole_catalogue_and_only_the_catalogue()
        {
            IReadOnlyList<string> names = Sfx.AllNames;
            IReadOnlyList<FieldInfo> fields = CatalogueFields();

            Assert.That(names.Count, Is.EqualTo(fields.Count), "one name per entry, no more and no fewer");

            HashSet<string> listed = new HashSet<string>(names);
            Assert.That(listed.Count, Is.EqualTo(names.Count), "no name should be listed twice");

            foreach (FieldInfo field in fields)
            {
                Assert.That(listed.Contains(field.Name), Is.True, $"'{field.Name}' is missing from AllNames");
            }

            foreach (string name in names)
            {
                Assert.That(Sfx.TryFind(name, out AudioEvent entry), Is.True, $"'{name}' is listed but unfindable");
                Assert.That(entry.IsValid, Is.True, $"'{name}' is listed but resolves to nothing playable");
            }
        }

        [Test]
        public void The_cheer_the_clip_fires_is_in_the_catalogue()
        {
            Assert.That(Sfx.CharacterCheer.Path, Is.EqualTo("event:/SFX/Character/Cheer"));
            Assert.That(Sfx.TryFind("CharacterCheer", out AudioEvent byName), Is.True);
            Assert.That(Sfx.TryFind("event:/SFX/Character/Cheer", out AudioEvent byPath), Is.True);
            Assert.That(byName.Path, Is.EqualTo(byPath.Path));
        }

        [Test]
        public void An_unknown_name_is_reported_once_and_then_swallowed()
        {
            CharacterAudioRelay relay = NewRelay("Mistyped");

            AnimationEvent evt = NamedEvent("CharacterFootstepTypoOne");

            LogAssert.Expect(LogType.Error, new Regex("CharacterFootstepTypoOne"));

            for (int i = 0; i < 20; i++)
            {
                relay.PostAudio(evt);
            }

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void An_event_that_names_no_sound_at_all_is_reported_too()
        {
            CharacterAudioRelay relay = NewRelay("Nameless");

            LogAssert.Expect(LogType.Error, new Regex("no sound named"));

            relay.PostAudio(NamedEvent(string.Empty));

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Nothing_an_animation_event_can_be_makes_the_relay_throw()
        {
            CharacterAudioRelay relay = NewRelay("Robust");

            LogAssert.Expect(LogType.Error, new Regex("CharacterFootstepTypoTwo"));

            Assert.DoesNotThrow(() => relay.PostAudio(null));
            Assert.DoesNotThrow(() => relay.PostAudio(NamedEvent("CharacterFootstepTypoTwo")));
            Assert.DoesNotThrow(() => relay.PostAudio(NamedEvent("CharacterFootstepTypoTwo", 4)));
            Assert.DoesNotThrow(() => relay.PostAudio(NamedEvent("CharacterFootstepTypoTwo", int.MinValue)));
        }

        [Test]
        public void The_method_animation_events_name_is_the_one_this_component_has()
        {
            Assert.That(
                typeof(CharacterAudioRelay).GetMethod(
                    CharacterAudioRelay.PostAudioMethod,
                    BindingFlags.Public | BindingFlags.Instance),
                Is.Not.Null,
                $"CharacterAudioRelay has no public {CharacterAudioRelay.PostAudioMethod} for a clip to call");
        }

        [Test]
        public void An_animated_character_gets_exactly_one_relay()
        {
            Transform character = NewLiveCharacter("Driven");

            CharacterAnimation.Attach(character);
            CharacterAnimation.Attach(character);

            Assert.That(
                character.GetComponents<CharacterAudioRelay>().Length,
                Is.EqualTo(1),
                "a second Attach must not leave a character posting every footstep twice");
        }

        [Test]
        public void A_character_that_cannot_be_animated_is_left_alone()
        {
            bool generated =
                Resources.Load<RuntimeAnimatorController>(CharacterAnimation.ControllerResource) != null;

            Transform character = NewCharacter("Plain");
            CharacterAnimation.Attach(character);

            Assert.That(
                character.GetComponent<CharacterAudioRelay>() != null,
                Is.EqualTo(generated),
                "no controller means nothing left behind on the character");
        }

        private static IReadOnlyList<FieldInfo> CatalogueFields()
        {
            List<FieldInfo> fields = new List<FieldInfo>();

            foreach (FieldInfo field in typeof(Sfx).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType == typeof(AudioEvent))
                {
                    fields.Add(field);
                }
            }

            return fields;
        }

        private static AnimationEvent NamedEvent(string sound, int step = -1)
        {
            return new AnimationEvent
            {
                functionName = CharacterAudioRelay.PostAudioMethod,
                stringParameter = sound,
                intParameter = step,
            };
        }

        private CharacterAudioRelay NewRelay(string name)
        {
            return NewCharacter(name).gameObject.AddComponent<CharacterAudioRelay>();
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
            AnimatorController controller = new AnimatorController { name = "StandInOctopus" };
            controllers.Add(controller);

            character.gameObject.AddComponent<Animator>().runtimeAnimatorController = controller;
            return character;
        }
    }
}
