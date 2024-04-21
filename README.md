# real-estate-notifier
Reads information about real estate in Belgrade from some popular websites and notifies the user via Telegram.

## Running locally

There are the prerequisites to install the needed tools to run the below.

```powershell
.\sqlite-tools-win-x64-3450300\sqlite3.exe realestate.db ".read .\real_estate.sql"
cd .\scrapers\
python .\nekretnine-scraper.py
cd ..\TelegramBot\
dotnet run
```
