using System.Collections.Generic;
using SushiParty.Audio;
using UnityEngine;

namespace SushiParty.Presentation
{
    public sealed class CharacterAudioRelay : MonoBehaviour
    {
        public const string PostAudioMethod = "PostAudio";
        private static readonly HashSet<string> reportedNames = new HashSet<string>();
        private static readonly HashSet<string> reportedFaults = new HashSet<string>();

        public static void ForgetReportedNames()
        {
            reportedNames.Clear();
            reportedFaults.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            ForgetReportedNames();
        }

        public void PostAudio(AnimationEvent evt)
        {
            try
            {
                if (evt == null)
                {
                    return;
                }

                if (!Sfx.TryFind(evt.stringParameter, out AudioEvent sound))
                {
                    ReportUnknownName(evt);
                    return;
                }

                if (evt.intParameter >= 0)
                {
                    GameAudio.PlayAt(sound, transform.position, Sfx.StepParameter, evt.intParameter);
                }
                else
                {
                    GameAudio.PlayAt(sound, transform.position);
                }
            }
            catch (System.Exception exception)
            {
                if (reportedFaults.Add(exception.Message))
                {
                    Debug.LogError($"[SushiParty] An animation event could not be posted: {exception.Message}");
                }
            }
        }

        private static void ReportUnknownName(AnimationEvent evt)
        {
            string asked = evt.stringParameter;
            string key = string.IsNullOrEmpty(asked) ? string.Empty : asked;

            if (!reportedNames.Add(key))
            {
                return;
            }

            if (string.IsNullOrEmpty(asked))
            {
                Debug.LogError(
                    $"[SushiParty] An animation event on {DescribeClip(evt)} calls "
                    + $"{PostAudioMethod} with no sound named in its String field. Nothing will play.");
                return;
            }

            Debug.LogError(
                $"[SushiParty] An animation event on {DescribeClip(evt)} asks for the sound "
                + $"'{asked}', which is not in Sfx. Nothing will play. Name one of Sfx's entries "
                + "— either the member name (CharacterFootstep) or the full Studio path "
                + "(event:/SFX/Character/Footstep).");
        }

        private static string DescribeClip(AnimationEvent evt)
        {
            if (!evt.isFiredByAnimator)
            {
                return "an unknown clip";
            }

            AnimationClip clip = evt.animatorClipInfo.clip;
            return clip != null ? $"'{clip.name}'" : "an unknown clip";
        }
    }
}
