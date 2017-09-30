using Discord;
using Discord.WebSocket;
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
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace PodcastPlayerDiscordBot
{
    public class Bot : IDisposable
    {
        private readonly static string prefix = "$pod ";

        private CommandService Commands { get; set; }
        private DiscordSocketClient Client { get; set; }
        private IServiceProvider Services { get; set; }

        private ISpeaker Speaker { get; set; }
        private Dictionary<string, PodcastFeed> Feeds { get; set; }
        private IFeedStorage Storage { get; set; }

        private Episode CurrentEpisode { get; set; }

        public Bot(IFeedStorage storage, ISpeaker speaker)
        {
            Speaker = speaker;
            Speaker.FinishedPlaying += StopPlaying;

            Storage = storage;
        }

        private void StopPlaying(object sender, FinishedPlayingEventArgs args)
        {
            CurrentEpisode = null;
            Task.WhenAll(
                Client.SetGameAsync(null),
                Client.SetStatusAsync(UserStatus.Idle));
        }

        public async Task Start(string token)
        {
            Commands = new CommandService(new CommandServiceConfig
            {
                DefaultRunMode = RunMode.Async
            });

            Client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                LogLevel = LogSeverity.Info
            });

            Services = new ServiceCollection()
                .AddSingleton(Client)
                .AddSingleton(Speaker)
                .AddSingleton(Storage)
                .BuildServiceProvider();

            await InstallCommands();

            Client.Log += OnLogMessage;

            Client.Connected += async () =>
            {
                await Client.SetStatusAsync(UserStatus.Idle);
            };

            await Client.LoginAsync(TokenType.Bot, token);
            await Client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        public async Task InstallCommands()
        {
            // Hook the MessageReceived Event into our Command Handler
            Client.MessageReceived += HandleCommand;
            // Discover all of the commands in this assembly and load them.
            await Commands.AddModulesAsync(Assembly.GetAssembly(typeof(Commands.Info)));
        }

        public async Task HandleCommand(SocketMessage messageParam)
        {
            // Don't process the command if it was a System Message
            var message = messageParam as SocketUserMessage;
            if (message == null) return;
            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;
            // Determine if the message is a command, based on if it starts with the assigned prefix or a mention prefix
            if (!(message.HasStringPrefix(prefix, ref argPos) || message.HasMentionPrefix(Client.CurrentUser, ref argPos))) return;

            // Create a Command Context
            var context = new CommandContext(Client, message);
            // Execute the command. (result does not indicate a return value, 
            // rather an object stating if the command executed succesfully)
            var result = await Commands.ExecuteAsync(context, argPos, Services);
            if (!result.IsSuccess)
                await context.Channel.SendMessageAsync(result.ErrorReason);
        }

        private Task OnLogMessage(LogMessage msg)
        {
            //Color
            ConsoleColor color;
            switch (msg.Severity)
            {
                case LogSeverity.Error: color = ConsoleColor.Red; break;
                case LogSeverity.Warning: color = ConsoleColor.Yellow; break;
                case LogSeverity.Info: color = ConsoleColor.White; break;
                case LogSeverity.Verbose: color = ConsoleColor.Gray; break;
                case LogSeverity.Debug: default: color = ConsoleColor.DarkGray; break;
            }

            //Exception
            string exMessage;
            Exception ex = msg.Exception;
            if (ex != null)
            {
                while (ex is AggregateException && ex.InnerException != null)
                    ex = ex.InnerException;
                exMessage = ex.Message;
            }
            else
                exMessage = null;

            //Source
            string sourceName = msg.Source?.ToString();

            //Text
            string text;
            if (msg.Message == null)
            {
                text = exMessage ?? "";
                exMessage = null;
            }
            else
                text = msg.Message;

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

            return Task.FromResult(text);
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
