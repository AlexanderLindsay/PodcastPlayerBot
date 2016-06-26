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

        private bool IsPlaying = false;

        public Bot(string appName)
        {
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
                        Client.SetGame("Discord.Net");
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

        private void CreateCommands()
        {
            var service = Client.GetService<CommandService>();

            service.CreateCommand("info")
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

            service.CreateCommand("play")
                .Description("plays a podcast in the voice channel you are currently in")
                .Do(async (e) =>
                {
                    var channel = e.User.VoiceChannel;
                    if (channel == null)
                    {
                        await Reply(e, "Can't find voice channel.");
                    }

                    if (!IsPlaying)
                    {
                        IsPlaying = true;

                        var audioService = Client.GetService<AudioService>();
                        var audio = await audioService.Join(channel);
                        var channelCount = audioService.Config.Channels;

                        var url = "http://traffic.libsyn.com/theblacktapes/THE_BLACK_TAPES_EPISODE_207_-_Personal_Possessions.mp3";
                        var speaker = new Speaker(url);
                        ThreadPool.QueueUserWorkItem(delegate
                        {
                            speaker.Load(
                                (error) =>
                                {
                                    Reply(e, $"Error playing audio: {error}").Wait();
                                });
                        });

                        ThreadPool.QueueUserWorkItem(delegate
                        {
                            speaker.Play(channelCount,
                                (b, offset, count) =>
                                {
                                    audio.Send(b, offset, count);
                                });
                        });
                    }
                });
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
