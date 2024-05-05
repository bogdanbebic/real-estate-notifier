from azure.cosmos import CosmosClient
from azure.identity import DefaultAzureCredential
from bs4 import BeautifulSoup
from datetime import datetime, timedelta, timezone
import logging
import requests
import time
import uuid

logging.basicConfig(
    format='%(asctime)s %(levelname)s: %(module)s[%(process)d] %(message)s',
    datefmt='%Y-%m-%d %H:%M:%S',
    level=logging.INFO)

logging.Formatter.converter = time.gmtime

class NekretnineScraper:
    def __init__(self):
        self.url = 'https://nekretnine.rs'
        self.location_prefixes_to_ignore = tuple([
            'Konjarnik',
            'Voždovac',
            'Višnjička',
            'Šumice',
            'Dušanovac',
            'Zemun',
            'Bežanijska kosa',
            'Vidikovac',
            'Mirijevo',
            'Miljakovac',
            'Lekino brdo',
            'Cerak',
            'Karaburma',
            'Novi Beograd Blok 4',
            'Novi Beograd Blok 5',
            'Novi Beograd Blok 6',
            'Novi Beograd Blok 7',
            'Novi Beograd Blok 1 (Fontana)',
            'Novi Beograd Blok 9a',
            'Novi Beograd Blok 11',
            'Novi Beograd Blok 28',
            'Novi Beograd Blok 34',
            'Novi Beograd Blok 37',
            'Novi Beograd Blok 38',
            'Rakovica',
            'Banjica',
            'Banovo brdo',
            'Žarkovo',
            'Tošin bunar',
            'Košutnjak',
            'Denkova bašta',
            'Medaković',
            'Stepa Stepanović',
            'Trošarina',
            'Braće Jerković',
            'Autokomanda',
            'Sremčica',
            'Čukarica',
            'Čukarička padina',
            'Labudovo brdo, Beograd, Srbija',
            'Batajnica, Beograd, Srbija',
        ])

    def get_next_page_link(self, soup):
        next_page_buttons = soup.find_all(class_='next-article-button')
        if not next_page_buttons:
            return None

        return self.url + next_page_buttons[0]['href']

    def parse_page(self, soup):
        parsed = []
        for element in soup.find_all(class_='offer-adress'):
            parent_element_offer = element.parent.parent
            offer_relative_url = parent_element_offer.find('a')['href']
            offer_meta_info = parent_element_offer.find('div', class_='offer-meta-info').text
            offer_date = offer_meta_info.split('|')[0].strip()
            offer_location = parent_element_offer.find(class_='offer-location').text.strip()
            offer_price = parent_element_offer.find(class_='offer-price').find('span').text.strip()

            datetime_filter = datetime.now(timezone.utc) - timedelta(days=1) < datetime.strptime(offer_date, '%d.%m.%Y').replace(tzinfo=timezone.utc)
            if not datetime_filter:
                continue

            location_filter = not offer_location.startswith(self.location_prefixes_to_ignore) and \
                not offer_relative_url.startswith('/stambeni-objekti/stanovi/zemun') and \
                not "dr-ivana-ribara" in offer_relative_url

            if not location_filter:
                continue

            parsed.append({'id': str(uuid.uuid4()), 'name': 'Nekretnine', 'price': offer_price, 'location': offer_location, 'url': self.url + offer_relative_url, 'visited': 0})

        return parsed

    def scrape(self):
        scraped = []

        next_link = f'{self.url}/stambeni-objekti/stambeni-objekti/stanovi/izdavanje-prodaja/izdavanje/grad/beograd/vrsta-grejanja/centralno-grejanje/kvadratura/40_1000000/cena/1_700/na-spratu/2_3_4_5_6_nije-poslednji-sprat/lista/po-stranici/20/'
        # next_link = f'{self.url}/stambeni-objekti/stanovi/izdavanje-prodaja/prodaja/grad/beograd/uknjizeno/vrsta-grejanja/centralno-grejanje/kvadratura/45_1000000/cena/1_300000/na-spratu/2_3_4_5_6_nije-poslednji-sprat/lista/po-stranici/20/'

        while next_link is not None:
            logging.info(next_link)

            response = requests.get(next_link)
            if response.status_code != 200:
                raise(f'Failed to fetch the webpage, status code: {response.status_code}')

            soup = BeautifulSoup(response.content, 'html.parser')
            parsed = self.parse_page(soup)
            next_link = self.get_next_page_link(soup)

            scraped.extend(parsed)

            # Prevent website from figuring out this is a bot:
            # Adding sleep time adds human like behavior.
            time.sleep(5)

        return scraped


if __name__ == "__main__":
    scraper = NekretnineScraper()
    real_estate = scraper.scrape()
    logging.debug(real_estate)
    logging.info(len(real_estate))

    client = CosmosClient(
        'https://realestatenotifier-cosmosdb.documents.azure.com:443/',
        credential=DefaultAzureCredential())

    database = client.get_database_client('cosmosDB')
    container = database.get_container_client('Items')

    for item in real_estate:
        container.create_item(body=item)
