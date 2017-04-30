using System.Collections.Generic;
using System.Threading.Tasks;

namespace PodcastPlayerDiscordBot
{
    public interface IFeedStorage
    {
        Task<Dictionary<string, PodcastFeed>> GetFeedsAsync();
        Task<PodcastFeed> GetFeedAsync(string name);
        Task AddFeedAsync(string name, PodcastFeed feed);
    }
}
