using Discord.Audio;
using System;
using System.Threading.Tasks;

namespace PodcastPlayerDiscordBot
{
    public interface ISpeaker
    {
        Task StopAsync();
        Task<bool> IsPlayingAsync();
        Task PlayUrlAsync(Uri url, IAudioClient client);
        event EventHandler<FinishedPlayingEventArgs> FinishedPlaying;
    }
}
