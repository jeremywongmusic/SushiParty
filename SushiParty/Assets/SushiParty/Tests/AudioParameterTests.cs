using NUnit.Framework;
using UnityEngine.TestTools;
using SushiParty.Audio;

namespace SushiParty.Tests
{
    public sealed class AudioParameterTests
    {
        [Test]
        public void A_number_is_not_labelled()
        {
            AudioParameter parameter = AudioParameter.Number(Sfx.ForceParameter, 0.5f);

            Assert.That(parameter.IsValid, Is.True);
            Assert.That(parameter.IsLabelled, Is.False, "a number goes through setParameterByName");
            Assert.That(parameter.Value, Is.EqualTo(0.5f));
        }

        [Test]
        public void A_label_is_labelled()
        {
            AudioParameter parameter = AudioParameter.Labelled(Sfx.KindParameter, "Wasabi");

            Assert.That(parameter.IsLabelled, Is.True, "a label goes through setParameterByNameWithLabel");
            Assert.That(parameter.Label, Is.EqualTo("Wasabi"));
        }

        [Test]
        public void No_parameter_is_not_valid()
        {
            Assert.That(AudioParameter.None.IsValid, Is.False);
            Assert.That(AudioParameter.Number(null, 1f).IsValid, Is.False);
        }

        [Test]
        public void A_missing_label_is_dropped_rather_than_sent_as_an_empty_one()
        {
            LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("no label"));

            AudioParameter parameter = AudioParameter.Labelled(Sfx.KindParameter, null);

            Assert.That(parameter.IsValid, Is.False,
                "an empty label is a state that cannot exist, so the event plays at its " +
                "authored default instead of being handed one");
        }

        [Test]
        public void Seek_speed_travels_with_the_set_rather_than_the_parameter()
        {
            Assert.That(AudioParameter.Number(Sfx.SpeedParameter, 1f).IgnoreSeekSpeed, Is.False,
                "the usual caller is a per-frame drive, which wants the smoothing");
            Assert.That(AudioParameter.Number(Sfx.SpeedParameter, 1f, ignoreSeekSpeed: true).IgnoreSeekSpeed,
                Is.True);
        }

        [Test]
        public void It_prints_the_two_kinds_differently()
        {
            Assert.That(AudioParameter.Number("Force", 0.5f).ToString(), Does.Contain("0.50"));
            Assert.That(AudioParameter.Labelled("Kind", "Wasabi").ToString(), Does.Contain("\"Wasabi\""));
            Assert.That(AudioParameter.None.ToString(), Is.EqualTo("(none)"));
        }
    }
}
