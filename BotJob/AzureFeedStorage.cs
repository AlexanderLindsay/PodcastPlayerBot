using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using PodcastPlayerDiscordBot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace BotJob
{
    public class AzureFeedStorage : IFeedStorage
    {
        private DocumentClient _client;
        private Dictionary<string, PodcastFeed> _inMemory;
        private bool hasAll = false;

        private string databaseName = "Feeds";
        private string collectionName = "FeedCollection";

        public AzureFeedStorage(string url, string key)
        {
            _client = new DocumentClient(new Uri(url), key);
            _inMemory = new Dictionary<string, PodcastFeed>();
        }

        public async Task Initalize()
        {
            await _client.CreateDatabaseIfNotExistsAsync(new Database { Id = databaseName });
            await _client.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri(databaseName), new DocumentCollection { Id = collectionName });
        }

        public async Task AddFeedAsync(string name, PodcastFeed feed)
        {
            try
            {
                var feedDocument = new FeedDocument
                {
                    Name = name,
                    Url = feed.Url.ToString()
                };

                _inMemory.Add(name, feed);
                feed.Refresh();

                await _client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseName, collectionName), feedDocument);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw e;
            }
        }

        public Task<PodcastFeed> GetFeedAsync(string name)
        {
            try
            {
                PodcastFeed feed;

                if (_inMemory.ContainsKey(name))
                {
                    feed = _inMemory[name];
                    feed.Refresh();
                }
                else
                {
                    FeedOptions queryOptions = new FeedOptions { MaxItemCount = -1 };

                    var feeds = _client.CreateDocumentQuery<FeedDocument>(
                            UriFactory.CreateDocumentCollectionUri(databaseName, collectionName), queryOptions)
                            .Where(fd => fd.Name == name)
                            .ToList();

                    feed = new PodcastFeed(feeds.SingleOrDefault()?.Url);
                }

                if (feed != null && !_inMemory.ContainsKey(name))
                {
                    _inMemory.Add(name, feed);
                    feed.Refresh();
                }

                return Task.FromResult(feed);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw e;
            }
        }

        public Task<Dictionary<string, PodcastFeed>> GetFeedsAsync()
        {
            if (hasAll)
            {
                return Task.FromResult(_inMemory);
            }
            else
            {
                FeedOptions queryOptions = new FeedOptions { MaxItemCount = -1 };

                IQueryable<FeedDocument> feeds = _client.CreateDocumentQuery<FeedDocument>(
                        UriFactory.CreateDocumentCollectionUri(databaseName, collectionName), queryOptions);

                var allFeeds = feeds
                    .ToList();

                var dictionary = allFeeds.ToDictionary(fd => fd.Name,
                    fd =>
                    {
                        var feed = new PodcastFeed(fd.Url);
                        feed.Refresh();
                        return feed;
                    });

                _inMemory = dictionary;

                return Task.FromResult(dictionary);
            }
        }
    }
}
