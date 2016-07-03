using NAudio.Wave;
using System;
using System.Configuration;
using System.Threading;

namespace PodcastPlayerDiscordBot
{
    class Program
    {
        static void Main(string[] args)
        {
            var token = ConfigurationManager.AppSettings["token"];
            var file = ConfigurationManager.AppSettings["feedFile"];

            var storage = new FileFeedStorage(file);

            using (var bot = new Bot("Podcast Bot", storage))
            {
                bot.Start(token);
            }
        }
    }
}
