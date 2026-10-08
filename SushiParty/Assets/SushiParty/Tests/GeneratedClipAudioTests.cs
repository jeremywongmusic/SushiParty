using System.Collections.Generic;
using NUnit.Framework;
using SushiParty.Audio;
using SushiParty.Presentation;
using UnityEditor;
using UnityEngine;

namespace SushiParty.Tests
{
    public sealed class GeneratedClipAudioTests
    {
        private const string ClipFolder = "Assets/SushiParty/Resources/Animation";

        private static List<AnimationClip> LoadGeneratedClips()
        {
            List<AnimationClip> clips = new List<AnimationClip>();

            if (!AssetDatabase.IsValidFolder(ClipFolder))
            {
                return clips;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { ClipFolder }))
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    AssetDatabase.GUIDToAssetPath(guid));

                if (clip != null)
                {
                    clips.Add(clip);
                }
            }

            return clips;
        }

        private static List<AnimationClip> RequireClips()
        {
            List<AnimationClip> clips = LoadGeneratedClips();

            if (clips.Count == 0)
            {
                Assert.Ignore("Nothing generated in this clone.");
            }

            return clips;
        }

        [Test]
        public void Every_cue_on_every_clip_names_a_sound_that_exists()
        {
            List<string> broken = new List<string>();

            foreach (AnimationClip clip in RequireClips())
            {
                foreach (AnimationEvent placed in AnimationUtility.GetAnimationEvents(clip))
                {
                    if (placed.functionName != CharacterAudioRelay.PostAudioMethod)
                    {
                        continue;
                    }

                    if (!Sfx.TryFind(placed.stringParameter, out AudioEvent _))
                    {
                        broken.Add($"{clip.name} at {placed.time:0.000}s names \"{placed.stringParameter}\"");
                    }
                }
            }

            Assert.That(
                broken,
                Is.Empty,
                "these clips name sounds that are not in the Sfx catalogue:\n  " + string.Join("\n  ", broken));
        }

        [Test]
        public void No_clip_calls_a_method_that_is_not_the_relay()
        {
            List<string> strays = new List<string>();

            foreach (AnimationClip clip in RequireClips())
            {
                foreach (AnimationEvent placed in AnimationUtility.GetAnimationEvents(clip))
                {
                    if (placed.functionName != CharacterAudioRelay.PostAudioMethod)
                    {
                        strays.Add($"{clip.name} at {placed.time:0.000}s calls \"{placed.functionName}\"");
                    }
                }
            }

            Assert.That(
                strays,
                Is.Empty,
                "only " + CharacterAudioRelay.PostAudioMethod + " has a receiver:\n  " + string.Join("\n  ", strays));
        }

        [Test]
        public void No_cue_sits_outside_the_clip_that_carries_it()
        {
            List<string> stranded = new List<string>();

            foreach (AnimationClip clip in RequireClips())
            {
                foreach (AnimationEvent placed in AnimationUtility.GetAnimationEvents(clip))
                {
                    if (placed.time < 0f || placed.time > clip.length)
                    {
                        stranded.Add($"{clip.name} at {placed.time:0.000}s (clip is {clip.length:0.000}s)");
                    }
                }
            }

            Assert.That(stranded, Is.Empty, "cues that can never fire:\n  " + string.Join("\n  ", stranded));
        }

        [Test]
        public void The_cheer_clip_carries_its_cue()
        {
            AnimationClip cheer = null;
            foreach (AnimationClip clip in RequireClips())
            {
                if (clip.name == "Cheer")
                {
                    cheer = clip;
                    break;
                }
            }

            Assert.That(cheer, Is.Not.Null, "the generator should have written a Cheer clip");

            bool found = false;
            foreach (AnimationEvent placed in AnimationUtility.GetAnimationEvents(cheer))
            {
                if (placed.functionName == CharacterAudioRelay.PostAudioMethod
                    && Sfx.TryFind(placed.stringParameter, out AudioEvent resolved)
                    && resolved.Path == Sfx.CharacterCheer.Path)
                {
                    found = true;
                    break;
                }
            }

            Assert.That(found, Is.True, "Cheer should fire Sfx.CharacterCheer");
        }

        [Test]
        public void The_clips_that_code_already_sounds_stay_silent()
        {
            string[] ownedByCode = { "Walk", "Jump", "Land", "Pound" };
            List<string> doubled = new List<string>();

            foreach (AnimationClip clip in RequireClips())
            {
                if (System.Array.IndexOf(ownedByCode, clip.name) < 0)
                {
                    continue;
                }

                foreach (AnimationEvent placed in AnimationUtility.GetAnimationEvents(clip))
                {
                    if (placed.functionName == CharacterAudioRelay.PostAudioMethod)
                    {
                        doubled.Add($"{clip.name} fires \"{placed.stringParameter}\"");
                    }
                }
            }

            Assert.That(
                doubled,
                Is.Empty,
                "C# already plays these; a cue here would double them:\n  " + string.Join("\n  ", doubled));
        }
    }
}
