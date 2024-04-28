from bs4 import BeautifulSoup
from configparser import ConfigParser
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

website_base_url = 'https://nekretnine.rs'

# URL of the webpage to fetch
nextLink = f'{website_base_url}/stambeni-objekti/stambeni-objekti/stanovi/izdavanje-prodaja/izdavanje/grad/beograd/vrsta-grejanja/centralno-grejanje/kvadratura/40_1000000/cena/1_700/na-spratu/2_3_4_5_6_nije-poslednji-sprat/lista/po-stranici/20/'

def parse_page(soup, dbFilePath, prefixList):
    with sqlite3.connect(dbFilePath) as conn:
        cursor = conn.cursor()

        rowCount = 0

        for element in soup.find_all(class_='offer-adress'):
            parent_element_offer = element.parent.parent
            offer_title = "TODO"
            # offer_title = parent_element_offer.find('a').text
            offer_relative_url = parent_element_offer.find('a')['href']
            offer_meta_info = parent_element_offer.find('div', class_='offer-meta-info').text
            offer_date = offer_meta_info.split('|')[0].strip()
            offer_location = parent_element_offer.find(class_='offer-location').text.strip()

            offer_price = "TODO"
            # offer_price = parent_element_offer.find(class_='offer-price').text.strip()

            datetime_filter = datetime.now(timezone.utc) - timedelta(days=1) < datetime.strptime(offer_date, '%d.%m.%Y').replace(tzinfo=timezone.utc)
            if not datetime_filter:
                continue

            location_filter = not offer_location.startswith(tuple(prefixList)) and \
                not offer_relative_url.startswith('/stambeni-objekti/stanovi/zemun') and \
                not "dr-ivana-ribara" in offer_relative_url

            if not location_filter:
                continue

            data_dict = {'name': offer_title, 'price': offer_price, 'location': offer_location, 'url': website_base_url + offer_relative_url}
            cursor.execute('''INSERT OR IGNORE INTO RealEstate (Name, Price, Location, URL) VALUES (:name, :price, :location, :url)''', data_dict)
            rowCount += 1

        conn.commit()

        logging.info(f"Rows processed: {rowCount}")


if __name__ == "__main__":
    script_dir = os.path.dirname(os.path.realpath(__file__))

    config = ConfigParser()
    config.read(os.path.join(script_dir, 'config.ini'), encoding='utf-8')
    config.read(os.path.join(script_dir, 'config.local.ini'), encoding='utf-8')

    dbPath = os.path.join(script_dir, '../realestate.db')
    prefixList = config.get('main', 'prefixList').split(',\n')

    while True:
        logging.info(nextLink)

        response = requests.get(nextLink)

        if response.status_code != 200:
            raise(f'Failed to fetch the webpage, status code: {response.status_code}')

        soup = BeautifulSoup(response.content, 'html.parser')

        nextArticleButtons = soup.find_all(class_='next-article-button')
        if not nextArticleButtons:
            break

        nextLink = website_base_url + nextArticleButtons[0]['href']

        parse_page(soup, dbPath, prefixList)

        # Prevent website from figuring out this is a bot:
        # Adding sleep time adds human like behavior.
        time.sleep(5)
