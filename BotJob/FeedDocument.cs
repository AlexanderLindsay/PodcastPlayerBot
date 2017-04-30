using Newtonsoft.Json;
using PodcastPlayerDiscordBot;

namespace BotJob
{
    public class FeedDocument
    {
        public string Id { get; set; }
        public PodcastFeed Feed { get; set; }
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
