FROM alpine:latest

COPY scrapers/ /app/scrapers/

COPY requirements.txt /app/

WORKDIR /app/

RUN apk add --no-cache python3 py3-pip

RUN python3 -m venv /venv && \
    . /venv/bin/activate && \
    pip3 install --upgrade pip && \
    pip3 install -r requirements.txt

RUN echo "0 * * * * . /venv/bin/activate && python /app/scrapers/nekretnine-scraper.py" > /etc/crontabs/root

CMD ["crond", "-f"]
