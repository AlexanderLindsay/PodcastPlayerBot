using Newtonsoft.Json;
using PodcastPlayerDiscordBot;
using System;

namespace BotJob
{
    public class FeedDocument
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
