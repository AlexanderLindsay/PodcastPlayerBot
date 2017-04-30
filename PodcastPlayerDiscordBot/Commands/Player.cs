using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PodcastPlayerDiscordBot.Commands
{
    public class Player : ModuleBase
    {
        private readonly DiscordSocketClient _client;
        private readonly Speaker _speaker;

        // Dependencies can be injected via the constructor
        public Player(DiscordSocketClient client, Speaker speaker)
        {
            _client = client;
            _speaker = speaker;
        }

        // ~play url <url>
        [RequireUserPermission(GuildPermission.ManageMessages), Command("play url"), Summary("plays a podcast in the voice channel you are currently in")]
        public async Task PlayUrl([Summary("The url to play")] string url)
        {
            var uri = new Uri(url);
            await PlayUrl(uri, url.ToString());
        }

        // ~stop
        [RequireUserPermission(GuildPermission.ManageMessages), Command("stop"), Alias("stap"), Summary("halts the playing of the podcast")]
        public Task Stop()
        {
            _speaker.Stop();
            return Task.FromResult(false);
        }

        // ~leave
        [RequireUserPermission(GuildPermission.ManageMessages), Command("leave"), Alias("go away"), Summary("the bot will leave the voice channel it is currently in")]
        public async Task Leave()
        {
            var user = Context.Message.Author as IGuildUser;
            var channel = user?.VoiceChannel;
            if (channel == null)
            {
                await ReplyAsync("Can't find voice channel.");
                return;
            }

            _speaker.Stop();

            var audio = await channel.ConnectAsync();
            await audio.StopAsync();
        }

        private async Task<bool> PlayUrl(Uri url, string name)
        {
            var user = Context.Message.Author as IGuildUser;
            var channel = user?.VoiceChannel;
            if (channel == null)
            {
                await ReplyAsync("Can't find voice channel.");
                return false;
            }

            if (url == null)
            {
                await ReplyAsync("Can't find url for episode.");
                return false;
            }

            if (!_speaker.IsSpeaking())
            {
                var audio = await channel.ConnectAsync();
                var audioStream = audio.CreatePCMStream(AudioApplication.Mixed, 1920);

                await _client.SetGameAsync(name);
                await _client.SetStatusAsync(UserStatus.Online);

                _speaker.Load(url,
                    (error) =>
                    {
                        ReplyAsync($"Error playing audio: {error}").Wait();
                    });

                _speaker.Play(2,
                    (b, offset, count) =>
                    {
                        audioStream.WriteAsync(b, offset, count).Wait();
                    });

                return true;
            }

            return false;
        }
    }
}
