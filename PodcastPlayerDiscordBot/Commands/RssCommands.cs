using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PodcastPlayerDiscordBot.Commands
{
    [Group("rss")]
    public class RssCommands : ModuleBase
    {
        private readonly DiscordSocketClient _client;
        private readonly ISpeaker _speaker;
        private readonly IFeedStorage _feedStorage;

        private string LastFeed { get; set; }
        private int LastEpisodeNumber { get; set; }
        private Episode CurrentEpisode { get; set; }

        // Dependencies can be injected via the constructor
        public RssCommands(DiscordSocketClient client, ISpeaker speaker, IFeedStorage feedStorage)
        {
            _client = client;
            _speaker = speaker;
            _feedStorage = feedStorage;
        }

        // ~add url 
        [RequireUserPermission(GuildPermission.ManageMessages), Command("add"), Summary("adds a rss feed url to the bot")]
        public async Task AddFeed([Summary("The name to give the feed")]string name,
            [Summary("The url of the rss feed")] string url)
        {
            var feed = new PodcastFeed(url);

            await _feedStorage.AddFeedAsync(name, feed);

            await ReplyAsync("Feed added");
        }

        // ~info -> information about the feed
        [RequireUserPermission(GuildPermission.ManageMessages), Command("info"), Summary("displays information about an rss feed")]
        public async Task Info([Summary("The name of the feed")]string name)
        {
            var feed = await _feedStorage.GetFeedAsync(name);
            if (feed != null)
            {
                await ReplyAsync($"{feed}");
            }
            else
            {
                await ReplyAsync("No feed by that name");
            }
        }

        // ~list -> lists current podcast feeds
        [RequireUserPermission(GuildPermission.ManageMessages), Command("list"), Summary("lists rss feeds")]
        public async Task ListFeeds()
        {
            var feeds = await _feedStorage.GetFeedsAsync();
            if (feeds.Count == 0)
            {
                await ReplyAsync("No feeds");
                return;
            }

            var builder = new StringBuilder();

            foreach (var feed in feeds.Keys)
            {
                builder.AppendLine(feed);
            }

            await ReplyAsync(builder.ToString());
        }

        // ~episodes name -> list of episodes in the feed
        [RequireUserPermission(GuildPermission.ManageMessages), Command("episodes"), Summary("lists episodes in an rss feed")]
        public async Task ListEpisodes([Summary("The name of the feed")]string name)
        {
            var feed = await _feedStorage.GetFeedAsync(name);
            if (feed != null)
            {
                var items = feed.ListItems();

                var builder = new StringBuilder();

                var number = feed.NumberOfEpisodes();
                builder.AppendLine($"Number of Episodes: {number}");
                builder.AppendLine();

                foreach (var item in feed.ListItems())
                {
                    builder.Append($"{number}. {item}");
                    number--;
                }

                var message = builder.ToString();
                if (message.Length > 2000)
                {
                    message = $"{message.Substring(0, 1997)}...";
                }

                await ReplyAsync(message);
            }
            else
            {
                await ReplyAsync("No feed by that name");
            }
        }

        // ~what -> episode description
        [RequireUserPermission(GuildPermission.ManageMessages), Command("what"),
            Alias("what is this", "current", "playing"),
            Summary("provides information on the currenly playing episode")]
        public async Task What()
        {
            if (CurrentEpisode == null)
            {
                await ReplyAsync("No episode currently playing");
                return;
            }

            await ReplyAsync(CurrentEpisode.Describe());
        }

        // ~play last -> plays the last episode
        [RequireUserPermission(GuildPermission.ManageMessages), Command("play last"),
            Alias("play latest", "play current"),
            Summary("plays the last episode of the given rss feed")]
        public async Task PlayLast([Summary("The name of the feed")]string name)
        {
            var feed = await _feedStorage.GetFeedAsync(name);
            if (feed != null)
            {
                var episodeNumber = feed.NumberOfEpisodes();
                Console.WriteLine(episodeNumber);
                await PlayEpisodeFromFeed(name, episodeNumber - 1);
            }
            else
            {
                await ReplyAsync("No feed by that name");
            }
        }

        // ~play next -> plays the next episode
        [RequireUserPermission(GuildPermission.ManageMessages), Command("play next"),
            Summary("plays the next episode of the given rss feed")]
        public async Task PlayNext([Summary("The name of the feed")]string name)
        {
            await PlayEpisodeFromFeed(LastFeed, LastEpisodeNumber + 1);
        }

        // ~play feed episode -> plays specified episode from the specified podcast feed
        [RequireUserPermission(GuildPermission.ManageMessages), Command("play"),
            Alias("play episode", "episode", "e", "p"),
            Summary("play the given episode of the given rss feed")]
        public async Task PlayEpisode([Summary("The name of the feed")]string name,
            [Summary("The episode number")]int episodeNumber)
        {
            await PlayEpisodeFromFeed(name, episodeNumber - 1);
        }

        // ~restart -> restarts the current podcast
        [RequireUserPermission(GuildPermission.ManageMessages), Command("restart"),
            Alias("repeat", "re"),
            Summary("restarts the currently playing episode")]
        public async Task Restart()
        {
            await PlayEpisodeFromFeed(LastFeed, LastEpisodeNumber);
        }

        private async Task PlayEpisodeFromFeed(string feedName, int episodeNumber)
        {
            var feed = await _feedStorage.GetFeedAsync(feedName);
            if (feed != null)
            {
                var episode = feed.ListItems().OrderBy(f => f.PublishedDate).ElementAtOrDefault(episodeNumber);
                if (episode == null)
                {
                    await ReplyAsync("No episode with that number");
                }

                if (await PlayUrl(episode.Link, episode.Name))
                {
                    await ReplyAsync($"Playing {episode}");
                    LastFeed = feedName;
                    LastEpisodeNumber = episodeNumber;
                    CurrentEpisode = episode;
                }
            }
            else
            {
                await ReplyAsync("No feed by that name");
            }
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

            if (!await _speaker.IsPlayingAsync())
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
