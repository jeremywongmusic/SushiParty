using System.Collections.Generic;

namespace SushiParty.Audio
{
    public interface IGlobalParameters
    {
        void Publish(AudioParameter parameter);
    }

    public sealed class GameAudioGlobals : IGlobalParameters
    {
        public void Publish(AudioParameter parameter)
        {
            if (!parameter.IsValid)
            {
                return;
            }

            if (parameter.IsLabelled)
            {
                GameAudio.SetGlobalLabel(parameter.Name, parameter.Label);
                return;
            }

            GameAudio.SetGlobal(parameter.Name, parameter.Value);
        }
    }

    public sealed class RecordedGlobals : IGlobalParameters
    {
        private readonly Dictionary<string, AudioParameter> published =
            new Dictionary<string, AudioParameter>();

        public void Publish(AudioParameter parameter)
        {
            if (!parameter.IsValid)
            {
                return;
            }

            published[parameter.Name] = parameter;
        }

        public int Count => published.Count;
        public bool Has(string parameter) => published.ContainsKey(parameter);
        public float Number(string parameter) => published[parameter].Value;
        public string Label(string parameter) => published[parameter].Label;
        public void Clear() => published.Clear();
    }
}
