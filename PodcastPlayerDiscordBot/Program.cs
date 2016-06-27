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

            using (var bot = new Bot("Podcast Bot"))
            {
                bot.Start(token);
            }
        }
    }
}
