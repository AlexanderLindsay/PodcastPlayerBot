using System.Collections.Generic;
using System.Threading.Tasks;

namespace PodcastPlayerDiscordBot
{
    public interface IFeedStorage
    {
        Dictionary<string, PodcastFeed> GetFeeds();
        Task AddFeed(string name, PodcastFeed feed);
    }
}
