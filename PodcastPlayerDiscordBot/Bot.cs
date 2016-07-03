using Discord;
using Discord.Commands;
using Discord.Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NAudio.Wave;
using System.Threading;

namespace PodcastPlayerDiscordBot
{
    public class Bot : IDisposable
    {
        private readonly static string prefix = "$pod";

        private DiscordClient Client { get; set; }

        private Speaker Speaker { get; set; }
        private Dictionary<string, PodcastFeed> Feeds { get; set; }
        private IFeedStorage Storage { get; set; }

        private string LastFeed { get; set; }
        private int LastEpisodeNumber { get; set; }

        private Episode CurrentEpisode { get; set; }

        public Bot(string appName, IFeedStorage storage)
        {
            Speaker = new Speaker();
            Speaker.FinishedPlaying += StopPlaying;

            Storage = storage;
            Feeds = storage.GetFeeds();

            Client = new DiscordClient(c =>
            {
                c.AppName = appName;
                c.MessageCacheSize = 0;
                c.LogLevel = LogSeverity.Info;
                c.LogHandler = OnLogMessage;
            });

            Client.UsingCommands(c =>
            {
                c.CustomPrefixHandler = (msg) =>
                {
                    if (msg.User.IsBot)
                        return -1;

                    var isMatch = Regex.IsMatch(msg.Text, $"^\\{prefix}");
                    if (isMatch)
                        return prefix.Length;

                    return -1;
                };
                c.AllowMentionPrefix = true;
                c.HelpMode = HelpMode.Public;
                c.ExecuteHandler = OnCommandExecuted;
                c.ErrorHandler = OnCommandError;
            });

            Client.UsingAudio(c =>
            {
                c.Mode = AudioMode.Outgoing;
            });

            CreateCommands();
        }

        public void Start(string token)
        {
            Client.ExecuteAndWait(async () =>
            {
                while (true)
                {
                    try
                    {
                        await Client.Connect(token);
                        Client.SetStatus(UserStatus.Idle);
                        break;
                    }
                    catch (Exception ex)
                    {
                        Client.Log.Error($"Login Failed", ex);
                        await Task.Delay(Client.Config.FailedReconnectDelay);
                    }
                }
            });
        }

        private void StopPlaying(object sender, FinishedPlayingEventArgs args)
        {
            CurrentEpisode = null;
            Client.SetGame(null);
            Client.SetStatus(UserStatus.Idle);
        }

        private void CreateCommands()
        {
            var service = Client.GetService<CommandService>();

            service.CreateCommand("info")
                .AddCheck(CheckPermissions)
                .Description("displays information about the bot")
                .Do(async (e) =>
                {
                    var botName = "Podcast Player Bot v0.1.0";
                    var discordVersion = typeof(DiscordClient).Assembly.GetName().Version;
                    var discordInfo = $"Build using Discord.NET {discordVersion}";
                    var helpInfo = "type `$pod help` for information on how to use the bot";

                    var msg = $"{botName}\n{discordInfo}\n{helpInfo}";
                    await Reply(e, msg);
                });

            service.CreateCommand("play url")
                .Alias("play")
                .AddCheck(CheckPermissions)
                .Description("plays a podcast in the voice channel you are currently in")
                .Parameter("url", ParameterType.Required)
                .Do(async (e) =>
                {
                    var href = e.GetArg("url");
                    var url = new Uri(href);

                    await PlayUrl(e, url, url.ToString());
                });

            service.CreateCommand("stop")
                .Alias("pause")
                .Alias("stap")
                .AddCheck(CheckPermissions)
                .Description("halts the playing of the podcast")
                .Do((e) =>
                {
                    Speaker.Stop();
                });

            service.CreateCommand("leave")
                .Alias("go away")
                .AddCheck(CheckPermissions)
                .Description("the bot will leave the voice channel it is currently in")
                .Do(async (e) =>
                {
                    var channel = e.User.VoiceChannel;
                    if (channel == null)
                    {
                        await Reply(e, "Can't find voice channel.");
                        return;
                    }

                    Speaker.Stop();

                    var audioService = Client.GetService<AudioService>();
                    await audioService.Leave(channel);
                });

            service.CreateCommand("what")
                .Alias("what is this")
                .Alias("current")
                .Alias("playing")
                .AddCheck(CheckPermissions)
                .Description("provides information on the currenly playing episode")
                .Do(async (e) =>
                {
                    if (CurrentEpisode == null)
                    {
                        await Reply(e, "No episode currently playing");
                        return;
                    }

                    await Reply(e, CurrentEpisode.Describe());
                });

            service.CreateCommand("rss add")
                .AddCheck(CheckPermissions)
                .Description("adds a rss feed url to the bot")
                .Parameter("name", ParameterType.Required)
                .Parameter("url", ParameterType.Required)
                .Do(async (e) =>
                {
                    var feed = new PodcastFeed(e.GetArg("url"));
                    await AddFeed(e.GetArg("name"), feed);

                    await Reply(e, "Feed added");
                });

            service.CreateCommand("rss info")
               .AddCheck(CheckPermissions)
               .Description("displays information about an rss feed")
               .Parameter("name", ParameterType.Required)
               .Do(async (e) =>
               {
                   var name = e.GetArg("name");
                   if (Feeds.ContainsKey(name))
                   {
                       var feed = Feeds[name];

                       await Reply(e, $"{feed}");
                   }
                   else
                   {
                       await Reply(e, "No feed by that name");
                   }
               });

            service.CreateCommand("rss list")
               .AddCheck(CheckPermissions)
               .Description("lists added rss feeds")
               .Do(async (e) =>
               {
                   if (Feeds.Count == 0)
                   {
                       await Reply(e, "No feeds");
                       return;
                   }

                   var builder = new StringBuilder();

                   foreach (var feed in Feeds.Keys)
                   {
                       builder.AppendLine(feed);
                   }

                   await Reply(e, builder.ToString());
               });

            service.CreateCommand("rss episodes")
                .Alias("episodes")
                .AddCheck(CheckPermissions)
                .Description("lists episodes in an rss feed")
                .Parameter("name", ParameterType.Required)
                .Do(async (e) =>
                {
                    var name = e.GetArg("name");
                    if (Feeds.ContainsKey(name))
                    {
                        var feed = Feeds[name];
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

                        await Reply(e, message);
                    }
                    else
                    {
                        await Reply(e, "No feed by that name");
                    }
                });

            service.CreateCommand("rss play last")
                .AddCheck(CheckPermissions)
                .Description("play the last episode of the given rss feed")
                .Parameter("name", ParameterType.Required)
                .Do(async (e) =>
                {
                    var name = e.GetArg("name");
                    if (Feeds.ContainsKey(name))
                    {
                        var feed = Feeds[name];

                        await PlayEpisodeFromFeed(e, e.GetArg("name"), feed.NumberOfEpisodes());
                    }
                    else
                    {
                        await Reply(e, "No feed by that name");
                    }
                });

            service.CreateCommand("rss play episode")
                .Alias("play episode")
                .Alias("episode")
                .Alias("e")
                .AddCheck(CheckPermissions)
                .Description("play the given episode of the given rss feed")
                .Parameter("name", ParameterType.Required)
                .Parameter("episode", ParameterType.Required)
                .Do(async (e) =>
                {
                    var number = e.GetArg("episode");
                    int index;
                    if (!int.TryParse(number, out index))
                    {
                        await Reply(e, "not a valid episode number");
                    }

                    var episodeNumber = index - 1;

                    await PlayEpisodeFromFeed(e, e.GetArg("name"), episodeNumber);
                });

            service.CreateCommand("next")
                .AddCheck(CheckPermissions)
                .Description("plays the next episode of the last played podcast")
                .Do(async (e) =>
                {
                    await PlayEpisodeFromFeed(e, LastFeed, LastEpisodeNumber + 1);
                });

            service.CreateCommand("restart")
                .AddCheck(CheckPermissions)
                .Description("restart the last played episode")
                .Do(async (e) =>
                {
                    await PlayEpisodeFromFeed(e, LastFeed, LastEpisodeNumber);
                });
        }

        private async Task AddFeed(string name, PodcastFeed feed)
        {
            Feeds.Add(name, feed);
            await Storage.AddFeed(name, feed);
        }

        private async Task PlayEpisodeFromFeed(CommandEventArgs e, string feedName, int episodeNumber)
        {
            if (Feeds.ContainsKey(feedName))
            {
                var feed = Feeds[feedName];

                var episode = feed.ListItems().OrderBy(f => f.PublishedDate).ElementAtOrDefault(episodeNumber);
                if (episode == null)
                {
                    await Reply(e, "No episode with that number");
                }

                if (await PlayUrl(e, episode.Link, episode.Name))
                {
                    await Reply(e, $"Playing {episode}");
                    LastFeed = feedName;
                    LastEpisodeNumber = episodeNumber;
                    CurrentEpisode = episode;
                }
            }
            else
            {
                await Reply(e, "No feed by that name");
            }
        }

        private async Task<bool> PlayUrl(CommandEventArgs e, Uri url, string name)
        {
            var channel = e.User.VoiceChannel;
            if (channel == null)
            {
                await Reply(e, "Can't find voice channel.");
                return false;
            }

            if (url == null)
            {
                await Reply(e, "Can't find url for episode.");
                return false;
            }

            if (!Speaker.IsSpeaking())
            {
                var audioService = Client.GetService<AudioService>();
                var audio = await audioService.Join(channel);
                var channelCount = audioService.Config.Channels;

                Client.Log.Info(e.User.Name, $"Playing url: {url}");
                Client.SetGame(name);
                Client.SetStatus(UserStatus.Online);

                Speaker.Load(url,
                    (error) =>
                    {
                        Reply(e, $"Error playing audio: {error}").Wait();
                    });

                Speaker.Play(channelCount,
                    (b, offset, count) =>
                    {
                        audio.Send(b, offset, count);
                    });

                return true;
            }

            return false;
        }

        private async Task Reply(Channel channel, string message)
        {
            if (message != null)
            {
                await channel.SendMessage(message);
            }
        }

        private async Task Reply(CommandErrorEventArgs e, string message)
        {
            await Reply(e.Channel, $"Error: {message}");
        }

        private async Task Reply(CommandEventArgs e, string message)
        {
            await Reply(e.Channel, message);
        }

        private bool CheckPermissions(Command command, User user, Channel channel)
        {
            return true;
        }

        private void OnCommandError(object sender, CommandErrorEventArgs e)
        {
            string msg = e.Exception?.Message;
            if (msg == null) //No exception - show a generic message
            {
                switch (e.ErrorType)
                {
                    case CommandErrorType.Exception:
                        msg = "Unknown error.";
                        break;
                    case CommandErrorType.BadPermissions:
                        msg = "You do not have permission to run this command.";
                        break;
                    case CommandErrorType.BadArgCount:
                        msg = "You provided the incorrect number of arguments for this command.";
                        break;
                    case CommandErrorType.InvalidInput:
                        msg = "Unable to parse your command, please check your input.";
                        break;
                    case CommandErrorType.UnknownCommand:
                        msg = "Unknown command.";
                        break;
                }
            }
            if (msg != null)
            {
                Reply(e, msg).Wait();
                Client.Log.Error("Command", msg);
            }
        }

        private void OnCommandExecuted(object sender, CommandEventArgs e)
        {
            Client.Log.Info("Command", $"{e.Command.Text} ({e.User.Name})");
        }

        private void OnLogMessage(object sender, LogMessageEventArgs e)
        {
            //Color
            ConsoleColor color;
            switch (e.Severity)
            {
                case LogSeverity.Error: color = ConsoleColor.Red; break;
                case LogSeverity.Warning: color = ConsoleColor.Yellow; break;
                case LogSeverity.Info: color = ConsoleColor.White; break;
                case LogSeverity.Verbose: color = ConsoleColor.Gray; break;
                case LogSeverity.Debug: default: color = ConsoleColor.DarkGray; break;
            }

            //Exception
            string exMessage;
            Exception ex = e.Exception;
            if (ex != null)
            {
                while (ex is AggregateException && ex.InnerException != null)
                    ex = ex.InnerException;
                exMessage = ex.Message;
            }
            else
                exMessage = null;

            //Source
            string sourceName = e.Source?.ToString();

            //Text
            string text;
            if (e.Message == null)
            {
                text = exMessage ?? "";
                exMessage = null;
            }
            else
                text = e.Message;

            //Build message
            StringBuilder builder = new StringBuilder(text.Length + (sourceName?.Length ?? 0) + (exMessage?.Length ?? 0) + 5);
            if (sourceName != null)
            {
                builder.Append('[');
                builder.Append(sourceName);
                builder.Append("] ");
            }
            for (int i = 0; i < text.Length; i++)
            {
                //Strip control chars
                char c = text[i];
                if (!char.IsControl(c))
                    builder.Append(c);
            }
            if (exMessage != null)
            {
                builder.Append(": ");
                builder.Append(exMessage);
            }

            text = builder.ToString();
            Console.ForegroundColor = color;
            Console.WriteLine(text);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Client.Dispose();
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
