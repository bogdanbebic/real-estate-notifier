using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace RealEstateNotifier
{
    public class NekretnineScraper(ILoggerFactory loggerFactory)
    {
        private readonly ILogger _logger = loggerFactory.CreateLogger<NekretnineScraper>();

        private const string Url = "https://nekretnine.rs";

        private const string QueryFilters = "izdavanje-prodaja/izdavanje/grad/beograd/vrsta-grejanja/centralno-grejanje/kvadratura/40_1000000/cena/1_700/na-spratu/2_3_4_5_6_nije-poslednji-sprat/lista/po-stranici/20";

        private static readonly ISet<string> LocationPrefixesToIgnore =
            new HashSet<string>
            {
                "Konjarnik",
                "Voždovac",
                "Višnjička",
                "Šumice",
                "Dušanovac",
                "Zemun",
                "Bežanijska kosa",
                "Vidikovac",
                "Mirijevo",
                "Miljakovac",
                "Lekino brdo",
                "Cerak",
                "Karaburma",
                "Novi Beograd Blok 4",
                "Novi Beograd Blok 5",
                "Novi Beograd Blok 6",
                "Novi Beograd Blok 7",
                "Novi Beograd Blok 1 (Fontana)",
                "Novi Beograd Blok 9a",
                "Novi Beograd Blok 11",
                "Novi Beograd Blok 28",
                "Novi Beograd Blok 34",
                "Novi Beograd Blok 37",
                "Novi Beograd Blok 38",
                "Rakovica",
                "Banjica",
                "Banovo brdo",
                "Žarkovo",
                "Tošin bunar",
                "Košutnjak",
                "Denkova bašta",
                "Medaković",
                "Stepa Stepanović",
                "Trošarina",
                "Braće Jerković",
                "Autokomanda",
                "Sremčica",
                "Čukarica",
                "Čukarička padina",
                "Labudovo brdo, Beograd, Srbija",
                "Batajnica, Beograd, Srbija",
            };

        [Function("NekretnineScraper")]
        public async Task Run([TimerTrigger("0 1-10 * * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }

            TokenCredential credential = new DefaultAzureCredential();;

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

            // To avoid the next link parsing and waiting inside the function,
            // we will trigger the function 10 times and parse the page index
            // which corresponds to the minute in which the function is being run on.
            foreach (RealEstate item in await this.ScrapePageAsync(pageIndex: DateTime.Now.Minute))
            {
                await container.CreateItemAsync(item);
            }
        }

        private async Task<IReadOnlyList<RealEstate>> ScrapePageAsync(int pageIndex)
        {
            _logger.LogInformation("Processing page with Index {Index}", pageIndex);

            string nextLink = $"{Url}/stambeni-objekti/stambeni-objekti/stanovi/{QueryFilters}/stranica/{pageIndex}/";

            HttpClient client = new();
            string responseBody = await client.GetStringAsync(nextLink);

            IBrowsingContext context = BrowsingContext.New(Configuration.Default);
            IDocument document = await context.OpenAsync(req => req.Content(responseBody));

            IHtmlCollection<IElement> offers = document.QuerySelectorAll(".offer-body");

            List<RealEstate> realEstate = [];

            foreach (IElement element in offers)
            {
                string? href = element.QuerySelector(".offer-title a")?.GetAttribute("href");
                string? dateText = element.QuerySelector(".offer-meta-info")?.TextContent.Split('|')[0].Trim();
                string? price = element.QuerySelector(".offer-price span")?.TextContent.Trim();
                string? location = element.QuerySelector(".offer-location")?.TextContent.Trim();

                if (!DateTime.TryParseExact(
                    dateText,
                    "dd.MM.yyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime parsedDate))
                {
                    // Date cannot be parsed, something is wrong with the formatting.
                    // We will skip this to avoid random offers without a valid date.
                    continue;
                }

                if (parsedDate.Date != DateTime.Today)
                {
                    // Too old offer or already parsed.
                    continue;
                }

                string nameString = "Nekretnine";
                string priceString = price ?? string.Empty;
                string locationString = location ?? string.Empty;
                string urlString = Url + (href ?? string.Empty);

                string hash = GetPersistableHash(
                    name: nameString,
                    price: priceString,
                    location: locationString,
                    url: urlString);

                realEstate.Add(
                    new RealEstate(
                        Id: hash,
                        Name: nameString,
                        Price: priceString,
                        Location: locationString,
                        Url: urlString,
                        Visited: 0));
            }

            _logger.LogInformation("Real Estate processed: {Count}", realEstate.Count);

            return realEstate.AsReadOnly();
        }

        public static string GetPersistableHash(
            string name,
            string price,
            string location,
            string url)
        {
            string data = string.Join(',', [name, price, location, url]);
            byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(data));
            return Encoding.ASCII.GetString(hash);
        }
    }
}
