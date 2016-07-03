using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

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

        public int NumberOfEpisodes()
        {
            if(Feed == null)
            {
                LoadFeed();
            }

            return Feed.Items.Count();
        }

        public IEnumerable<Episode> ListItems()
        {
            if (Feed == null)
            {
                LoadFeed();
            }

            foreach (var item in Feed.Items)
            {
                var durationExtension = item.ElementExtensions.FirstOrDefault(e => "duration".Equals(e.OuterName, StringComparison.OrdinalIgnoreCase));
                var imageExtension = item.ElementExtensions.FirstOrDefault(e => "image".Equals(e.OuterName, StringComparison.OrdinalIgnoreCase));

                var durationElement = durationExtension?.GetObject<XElement>();

                var imageElement = imageExtension?.GetObject<XElement>();
                var imageValue = imageElement?.FirstAttribute?.Value;
                Uri imageUrl = null;

                if (!string.IsNullOrEmpty(imageValue))
                {
                    imageUrl = new Uri(imageValue);
                }

                // Get rid of the tags
                var summary = Regex.Replace(item.Summary.Text, @"<.+?>", string.Empty);

                // Then decode the HTML entities
                summary = WebUtility.HtmlDecode(summary);

                yield return new Episode()
                {
                    Name = item.Title.Text,
                    Summary = summary,
                    PublishedDate = item.PublishDate,
                    Duration = durationElement?.Value,
                    Link = item.Links.FirstOrDefault(l => "audio/mpeg".Equals(l.MediaType, StringComparison.OrdinalIgnoreCase))?.Uri,
                    ImageLink = imageUrl
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
