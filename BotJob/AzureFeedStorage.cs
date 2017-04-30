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

        private string databaseName = "Feeds";
        private string collectionName = "FeedCollection";

        public AzureFeedStorage(string url, string key)
        {
            _client = new DocumentClient(new Uri(url), key);
        }

        public async Task Initalize()
        {
            await _client.CreateDatabaseIfNotExistsAsync(new Database { Id = databaseName });
            await _client.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri(databaseName), new DocumentCollection { Id = collectionName });
        }

        public async Task AddFeedAsync(string name, PodcastFeed feed)
        {
            var feedDocument = new FeedDocument
            {
                Id = name,
                Feed = feed
            };

            try
            {
                await _client.ReadDocumentAsync(UriFactory.CreateDocumentUri(databaseName, collectionName, feedDocument.Id));
            }
            catch (DocumentClientException de)
            {
                if (de.StatusCode == HttpStatusCode.NotFound)
                {
                    await _client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(databaseName, collectionName), feedDocument);
                }
                else
                {
                    throw;
                }
            }
        }

        public Task<PodcastFeed> GetFeedAsync(string name)
        {
            FeedOptions queryOptions = new FeedOptions { MaxItemCount = 1 };

            IQueryable<FeedDocument> feeds = _client.CreateDocumentQuery<FeedDocument>(
                    UriFactory.CreateDocumentCollectionUri(databaseName, collectionName), queryOptions)
                    .Where(fd => fd.Id == name);

            return Task.FromResult(feeds.SingleOrDefault().Feed);
        }

        public Task<Dictionary<string, PodcastFeed>> GetFeedsAsync()
        {
            FeedOptions queryOptions = new FeedOptions { MaxItemCount = -1 };

            IQueryable<FeedDocument> feeds = _client.CreateDocumentQuery<FeedDocument>(
                    UriFactory.CreateDocumentCollectionUri(databaseName, collectionName), queryOptions);

            return Task.FromResult(feeds.ToList().ToDictionary(fd => fd.Id, fd => fd.Feed));
        }
    }
}
