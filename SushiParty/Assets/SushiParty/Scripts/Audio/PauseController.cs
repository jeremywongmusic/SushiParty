using System.Collections.Generic;

namespace SushiParty.Audio
{
    public sealed class PauseController
    {
        private readonly IAudioMixer mixer;
        private readonly IReadOnlyList<MixerChannel> held;
        private readonly IGlobalParameters globals;
        private bool paused;
        private bool suspended;

        public PauseController(
            IAudioMixer mixer,
            IReadOnlyList<MixerChannel> held,
            IGlobalParameters globals = null)
        {
            this.mixer = mixer;
            this.held = held;
            this.globals = globals;
        }

        public bool IsPaused => paused;
        public bool IsHolding => paused && !suspended;
        public IReadOnlyList<MixerChannel> HeldChannels => held;

        public bool Pause()
        {
            if (paused)
            {
                return false;
            }

            paused = true;
            suspended = false;
            SetHeld(true);
            return true;
        }

        public bool Suspend()
        {
            if (!paused || suspended)
            {
                return false;
            }

            suspended = true;
            SetHeld(false);
            return true;
        }

        public bool Restore()
        {
            if (!suspended)
            {
                return false;
            }

            suspended = false;

            if (!paused)
            {
                return false;
            }

            SetHeld(true);
            return true;
        }

        public bool Resume()
        {
            if (!paused)
            {
                return false;
            }

            paused = false;

            bool wasSuspended = suspended;
            suspended = false;

            if (!wasSuspended)
            {
                SetHeld(false);
            }

            return true;
        }

        public bool Toggle()
        {
            if (paused)
            {
                Resume();
            }
            else
            {
                Pause();
            }

            return paused;
        }

        public void StopEverything()
        {
            Resume();

            for (int i = 0; i < Mixer.Buses.Count; i++)
            {
                mixer.StopAllEvents(Mixer.Buses[i], allowFadeout: false);
            }
        }

        private void PublishPaused()
        {
            globals?.Publish(AudioParameter.Number(Global.Paused, IsHolding ? 1f : 0f));
        }

        private void SetHeld(bool value)
        {
            PublishPaused();

            for (int i = 0; i < held.Count; i++)
            {
                if (held[i].IsBus)
                {
                    mixer.SetPaused(held[i], value);
                }
            }
        }
    }
}
