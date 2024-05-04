from bs4 import BeautifulSoup
from datetime import datetime, timedelta, timezone
import logging
import os
import requests
import sqlite3
import time

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

            offer_price = "TODO"
            # offer_price = parent_element_offer.find(class_='offer-price').text.strip()

            datetime_filter = datetime.now(timezone.utc) - timedelta(days=1) < datetime.strptime(offer_date, '%d.%m.%Y').replace(tzinfo=timezone.utc)
            if not datetime_filter:
                continue

            location_filter = not offer_location.startswith(self.location_prefixes_to_ignore) and \
                not offer_relative_url.startswith('/stambeni-objekti/stanovi/zemun') and \
                not "dr-ivana-ribara" in offer_relative_url

            if not location_filter:
                continue

            parsed.append({'name': 'Nekretnine', 'price': offer_price, 'location': offer_location, 'url': self.url + offer_relative_url})

        return parsed

    def scrape(self):
        scraped = []

        next_link = f'{self.url}/stambeni-objekti/stambeni-objekti/stanovi/izdavanje-prodaja/izdavanje/grad/beograd/vrsta-grejanja/centralno-grejanje/kvadratura/40_1000000/cena/1_700/na-spratu/2_3_4_5_6_nije-poslednji-sprat/lista/po-stranici/20/'

        while next_link is not None:
            logging.debug(next_link)

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
    script_dir = os.path.dirname(os.path.realpath(__file__))

    dbPath = os.path.join(script_dir, '../realestate-test.db')

    scraper = NekretnineScraper()
    real_estate = scraper.scrape()
    logging.debug(real_estate)

    with sqlite3.connect(dbPath) as conn:
        cursor = conn.cursor()
        cursor.executemany('''INSERT OR IGNORE INTO RealEstate (Name, Price, Location, URL) VALUES (:name, :price, :location, :url)''', real_estate)
        conn.commit()
