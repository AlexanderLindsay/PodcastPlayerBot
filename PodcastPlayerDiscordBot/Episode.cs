using System;
using System.Text;

namespace PodcastPlayerDiscordBot
{
    public class Episode
    {
        public string Name { get; set; }
        public string Summary { get; set; }
        public DateTimeOffset PublishedDate { get; set; }
        public Uri Link { get; set; }

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.AppendLine($"**{Name}**");
            builder.AppendLine($"published {PublishedDate.ToString("yyyy-MM-dd HH:mm")}");
            builder.AppendLine();

            return builder.ToString();
        }
    }
}
