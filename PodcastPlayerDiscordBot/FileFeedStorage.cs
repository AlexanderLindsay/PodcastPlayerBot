using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PodcastPlayerDiscordBot
{
    public class FileFeedStorage : IFeedStorage
    {
        private static readonly string Seperator = ": ";

        private string Path { get; set; }

        public FileFeedStorage(string path)
        {
            Path = path;
        }

        public async Task AddFeed(string name, PodcastFeed feed)
        {
            using (var writer = File.AppendText(Path))
            {
                await writer.WriteLineAsync($"{name}{Seperator}{feed.Url.ToString()}");
            }
        }

        public Dictionary<string, PodcastFeed> GetFeeds()
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
    }
}
