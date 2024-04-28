using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Data.Sqlite;
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

SecretClient secretClient = new(
    vaultUri: new Uri("https://realestatenotifier-vault.vault.azure.net/"),
    credential: new DefaultAzureCredential());

string telegramBotToken = secretClient.GetSecret(name: "TelegramBotToken").Value.Value;
int userChatID = int.Parse(secretClient.GetSecret(name: "TelegramChatID").Value.Value);

TelegramBotClient botClient = new(token: telegramBotToken);

string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "realestate.db");

logger.LogInformation($"Connecting to DB at path {dbPath}");

using SqliteConnection connection = new(connectionString: $"Data Source={dbPath}");
connection.Open();

using SqliteCommand command = new(
    commandText: "SELECT ID, Name, Price, Location, URL FROM RealEstate WHERE Visited = 0",
    connection: connection);

using SqliteDataReader reader = command.ExecuteReader();

int rowCount = 0;

while (reader.Read())
{
    int recordID = reader.GetInt32(reader.GetOrdinal("ID"));
    string name = reader["Name"]?.ToString() ?? string.Empty;
    string price = reader["Price"]?.ToString() ?? string.Empty;
    string location = reader["Location"]?.ToString() ?? string.Empty;
    string url = reader["URL"]?.ToString() ?? string.Empty;

    await botClient.SendTextMessageAsync(
        chatId: userChatID,
        text: $"{name}, {price}, {location}\n{url}\n"
    );

    using SqliteCommand updateCommand = new(
        commandText: "UPDATE RealEstate SET Visited = 1 WHERE ID = @RecordId",
        connection: connection);

    updateCommand.Parameters.AddWithValue("@RecordId", recordID);
    updateCommand.ExecuteNonQuery();

    rowCount++;
}

logger.LogInformation($"Rows processed: {rowCount}");
