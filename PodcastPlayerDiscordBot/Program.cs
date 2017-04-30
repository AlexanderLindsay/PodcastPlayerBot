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

            using (var bot = new Bot(storage))
            {
                bot.Start(token).GetAwaiter().GetResult();
            }
        }
    }
}
