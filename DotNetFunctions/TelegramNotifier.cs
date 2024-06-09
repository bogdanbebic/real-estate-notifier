using System;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace RealEstateNotifier
{
    public class TelegramNotifier(ILoggerFactory loggerFactory)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<TelegramNotifier>();

        [Function("TelegramNotifier")]
        public async Task Run([TimerTrigger("0 15 * * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }

            TokenCredential credential = new DefaultAzureCredential();

            SecretClient secretClient = new(
                vaultUri: new Uri("https://realestatenotifier-vault.vault.azure.net/"),
                credential: credential);

            string telegramBotToken = secretClient.GetSecret(name: "TelegramBotToken").Value.Value;
            int userChatID = int.Parse(secretClient.GetSecret(name: "TelegramChatID").Value.Value);

            TelegramBotClient botClient = new(token: telegramBotToken);

            CosmosClient cosmosClient = new(
                accountEndpoint: "https://realestatenotifier-cosmosdb.documents.azure.com:443/",
                tokenCredential: credential,
                clientOptions: new CosmosClientOptions()
                    {
                        SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase }
                    });

            Database database = cosmosClient.GetDatabase("cosmosDB");
            Container container = database.GetContainer("Items");

            _logger.LogInformation("Connected to Cosmos DB.");

            using FeedIterator<RealEstate> feed =
                container.GetItemQueryIterator<RealEstate>(
                    queryText: "SELECT * FROM Items WHERE Items.visited = 0");

            int rowCount = 0;

            while (feed.HasMoreResults)
            {
                FeedResponse<RealEstate> response = await feed.ReadNextAsync();
                foreach (RealEstate item in response)
                {
                    await botClient.SendTextMessageAsync(
                        chatId: userChatID,
                        text: $"{item.Name}: {item.Price}, {item.Location}\n{item.Url}\n");

                    RealEstate visitedItem = item with { Visited = 1 };
                    await container.ReplaceItemAsync(visitedItem, visitedItem.Id);

                    rowCount++;
                }
            }

            _logger.LogInformation("Rows processed: {Count}", rowCount);
        }
    }
}
