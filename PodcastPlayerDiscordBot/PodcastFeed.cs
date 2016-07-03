using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;

namespace PodcastPlayerDiscordBot
{
    public class PodcastFeed
    {
        public Uri Url { get; private set; }
        private SyndicationFeed Feed { get; set; }

        public PodcastFeed(string url)
        {
            Url = new Uri(url);
        }

        public PodcastFeed(Uri url)
        {
            Url = url;
        }

        private void LoadFeed()
        {
            using(var xmlReader = XmlReader.Create(Url.ToString()))
            {
                Feed = SyndicationFeed.Load(xmlReader);
            }
        }

        public void Refresh()
        {
            LoadFeed();
        }

        public IEnumerable<Episode> ListItems()
        {
            if (Feed == null)
            {
                LoadFeed();
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
                LoadFeed();
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
