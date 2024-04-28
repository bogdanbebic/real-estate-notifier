using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
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

IConfigurationBuilder builder = new ConfigurationBuilder()
    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

IConfiguration config = builder.Build();

TelegramBotClient botClient = new(token: config["AppSettings:TelegramBotToken"] ??
    throw new ArgumentNullException("TelegramBotToken"));

string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "realestate.db");

int userChatID = int.Parse(
    config["AppSettings:TelegramChatID"] ?? throw new ArgumentNullException("TelegramChatID"));

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
