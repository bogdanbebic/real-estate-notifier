using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

using ILoggerFactory loggerFactory =
    LoggerFactory.Create(builder =>
        builder.AddSimpleConsole(options =>
        {
            options.IncludeScopes = true;
            options.SingleLine = true;
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
            options.UseUtcTimestamp = true;
        }));

ILogger<Program> logger = loggerFactory.CreateLogger<Program>();

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

logger.LogInformation("Connected to Cosmos DB.");

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

logger.LogInformation("Rows processed: {rowCount}", rowCount);
