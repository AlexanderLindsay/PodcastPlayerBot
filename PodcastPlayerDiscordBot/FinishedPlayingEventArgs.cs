using System;

namespace PodcastPlayerDiscordBot
{
    public class FinishedPlayingEventArgs : EventArgs
    {
        public bool StoppedEarly { get; set; }

        public FinishedPlayingEventArgs(bool stoppedEarly = false)
        {
            StoppedEarly = true;
        }
    }
}