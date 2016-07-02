using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace PodcastPlayerDiscordBot
{
    public class PodcastFeed
    {
        private SyndicationFeed Feed { get; set; }

        public PodcastFeed() { }

        public static PodcastFeed Load(string url)
        {
            using (var xmlReader = XmlReader.Create(url))
            {
                return new PodcastFeed()
                {
                    Feed = SyndicationFeed.Load(xmlReader)
                };
            }
        }

        public IEnumerable<Episode> ListItems()
        {
            if (Feed == null)
            {
                yield break;
            }

            foreach (var item in Feed.Items)
            {
                yield return new Episode()
                {
                    Name = item.Title.Text,
                    Summary = item.Summary.Text,
                    PublishedDate = item.PublishDate,
                    Link = item.Links.FirstOrDefault(l => "audio/mpeg".Equals(l.MediaType, StringComparison.OrdinalIgnoreCase))?.Uri
                };
            }
        }

        public override string ToString()
        {
            if (Feed == null)
            {
                return "No feed loaded.";
            }

            var builder = new StringBuilder();

            builder.AppendLine($"**{Feed.Title.Text}**");

            if (Feed.Links != null && Feed.Links.Count > 0)
            {
                foreach (var link in Feed.Links)
                {
                    builder.AppendLine($"{link.Uri}");
                }
            }

            if (Feed.Authors != null && Feed.Authors.Any(a => !string.IsNullOrEmpty(a.Name)))
            {
                builder.AppendLine("Created by:");
                foreach (var author in Feed.Authors)
                {
                    builder.AppendLine($"    {author.Name}");
                }
            }

            if (Feed.ImageUrl != null)
            {
                builder.AppendLine($"{Feed.ImageUrl}");
            }

            builder.AppendLine();
            builder.AppendLine($"{Feed.Description.Text}");
            builder.AppendLine();
            builder.AppendLine($"*Last updated at {Feed.LastUpdatedTime.ToString("yyyy-MM-dd HH:mm")}*");
            builder.AppendLine($"*{Feed.Copyright.Text}*");

            return builder.ToString();
        }
    }
}
