using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PodcastPlayerDiscordBot
{
    public class FileFeedStorage : IFeedStorage
    {
        private static readonly string Seperator = ": ";
        private Dictionary<string, PodcastFeed> _inMemory;

        private string Path { get; set; }

        public FileFeedStorage(string path)
        {
            Path = path;
            _inMemory = LoadFeeds();
        }

        private Dictionary<string, PodcastFeed> LoadFeeds()
        {
            var dict = new Dictionary<string, PodcastFeed>();

            if (!File.Exists(Path))
            {
                return dict;
            }

            foreach (var line in File.ReadAllLines(Path))
            {
                var index = line.IndexOf(Seperator);
                var name = line.Substring(0, index);
                var url = line.Substring(index + Seperator.Length);

                dict.Add(name, new PodcastFeed(url));
            }

            return dict;
        }

        public async Task AddFeedAsync(string name, PodcastFeed feed)
        {
            _inMemory.Add(name, feed);
            using (var writer = File.AppendText(Path))
            {
                await writer.WriteLineAsync($"{name}{Seperator}{feed.Url.ToString()}");
            }
        }

        public Task<PodcastFeed> GetFeedAsync(string name)
        {
            PodcastFeed feed = null;
            if (_inMemory.ContainsKey(name))
            {
                feed = _inMemory[name];
            }

            return Task.FromResult(feed);
        }

        public Task<Dictionary<string, PodcastFeed>> GetFeedsAsync()
        {
            return Task.FromResult(LoadFeeds());
        }
    }
}
