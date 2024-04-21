using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;

IConfigurationBuilder builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

IConfiguration config = builder.Build();

TelegramBotClient botClient = new(token: config["AppSettings:TelegramBotToken"] ??
    throw new ArgumentNullException("TelegramBotToken"));

string dbPath = config["AppSettings:DatabasePath"] ??
    throw new ArgumentNullException("DatabasePath");

int userChatID = int.Parse(
    config["AppSettings:TelegramChatID"] ?? throw new ArgumentNullException("TelegramChatID"));

using SqliteConnection connection = new(connectionString: $"Data Source={dbPath}");
connection.Open();

string query = "SELECT Name, Price, Location, URL FROM RealEstate WHERE TimestampIngested > datetime('now', '-2 hours')";

using SqliteCommand command = new(query, connection);
using var reader = command.ExecuteReader();
while (reader.Read())
{
    string name = reader["Name"]?.ToString() ?? string.Empty;
    string price = reader["Price"]?.ToString() ?? string.Empty;
    string location = reader["Location"]?.ToString() ?? string.Empty;
    string url = reader["URL"]?.ToString() ?? string.Empty;

    await botClient.SendTextMessageAsync(
        chatId: userChatID,
        text: $"{name}, {price}, {location}\n{url}\n"
    );
}
