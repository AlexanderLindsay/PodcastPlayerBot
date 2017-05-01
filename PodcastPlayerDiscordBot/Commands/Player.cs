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
        private readonly ISpeaker _speaker;

        // Dependencies can be injected via the constructor
        public Player(DiscordSocketClient client, ISpeaker speaker)
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
        public async Task Stop()
        {
            await _speaker.StopAsync();
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

            await _speaker.StopAsync();
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

            if (! await _speaker.IsPlayingAsync())
            {
                var audio = await channel.ConnectAsync();

                await _client.SetGameAsync(name);
                await _client.SetStatusAsync(UserStatus.Online);

                await _speaker.PlayUrlAsync(url, audio);

                return true;
            }

            return false;
        }
    }
}
