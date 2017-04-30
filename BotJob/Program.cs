using Microsoft.Azure;
using PodcastPlayerDiscordBot;

namespace BotJob
{
    // To learn more about Microsoft Azure WebJobs SDK, please see https://go.microsoft.com/fwlink/?LinkID=320976
    class Program
    {
        static void Main()
        {
            var url = CloudConfigurationManager.GetSetting("documenturl");
            var key = CloudConfigurationManager.GetSetting("documentkey");
            var storage = new AzureFeedStorage(url, key);
            storage.Initalize().Wait();

            var token = CloudConfigurationManager.GetSetting("discordToken");
            new Bot(storage).Start(token).GetAwaiter().GetResult();
        }
    }
}
