using System;
using System.Text;

namespace PodcastPlayerDiscordBot
{
    public class Episode
    {
        public string Name { get; set; }
        public string Summary { get; set; }
        public string Duration { get; set; }
        public DateTimeOffset PublishedDate { get; set; }
        public Uri Link { get; set; }
        public Uri ImageLink { get; set; }

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.Append($"**{Name}**");

            if (!string.IsNullOrEmpty(Duration))
            {
                builder.Append($" [{Duration}]");
            }
            builder.AppendLine();

            builder.AppendLine($"published {PublishedDate.ToString("yyyy-MM-dd HH:mm")}");
            builder.AppendLine();

            return builder.ToString();
        }

        public string Describe()
        {
            var builder = new StringBuilder();

            builder.Append($"**{Name}**");
            if (!string.IsNullOrEmpty(Duration))
            {
                builder.Append($" [{Duration}]");
            }
            builder.AppendLine();

            builder.AppendLine($"{Link}");

            if (ImageLink != null)
            {
                builder.AppendLine($"{ImageLink}");
            }

            builder.AppendLine($"published {PublishedDate.ToString("yyyy-MM-dd HH:mm")}");
            builder.AppendLine();

            builder.AppendLine(Summary);

            return builder.ToString();
        }
    }
}
