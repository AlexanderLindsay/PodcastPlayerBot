using System.Configuration;

namespace PodcastPlayerDiscordBot
{
    class Program
    {
        static void Main(string[] args)
        {
            var token = ConfigurationManager.AppSettings["token"];
            var file = ConfigurationManager.AppSettings["feedFile"];

            var storage = new FileFeedStorage(file);
            var speaker = new WindowsSpeaker();

            using (var bot = new Bot(storage, speaker))
            {
                bot.Start(token).GetAwaiter().GetResult();
            }
        }
    }
}
