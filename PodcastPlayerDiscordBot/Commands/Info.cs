using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PodcastPlayerDiscordBot.Commands
{
    public class Info : ModuleBase
    {
        private readonly CommandService _commands;

        // Dependencies can be injected via the constructor
        public Info(CommandService commands)
        {
            _commands = commands;
        }

        // ~info
        [Command("info"), Summary("displays information about the bot")]
        public async Task GetInfo()
        {
            var botName = "Podcast Player Bot v0.2.0";
            var discordVersion = typeof(ModuleBase).Assembly.GetName().Version;
            var discordInfo = $"Build using Discord.NET {discordVersion}";
            var helpInfo = "type `$pod help` for information on how to use the bot";

            var msg = $"{botName}\n{discordInfo}\n{helpInfo}";
            await ReplyAsync(msg);
        }

        // ~help
        [Command("help"), Summary("displays commands")]
        public async Task GetHelp()
        {
            var builder = new StringBuilder();

            foreach(var command in _commands.Commands)
            {
                builder.AppendLine("------------------------------");
				
				builder.Append("**");

                if (command.Module.Name == "rss")
                {
                    builder.Append($"{command.Module.Name} ");
                }

                builder.AppendLine($"{command.Name}**");
                builder.AppendLine($"*{command.Summary}*");
				
                foreach(var parameter in command.Parameters)
                {
                    var requiredFlag = parameter.IsOptional ? "" : " *required*";
                    builder.AppendLine($"{parameter.Name}{requiredFlag}: {parameter.Summary}");
                }
            }

            var msg = builder.ToString();
            await ReplyAsync(msg);
        }
    }
}
