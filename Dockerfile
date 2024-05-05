FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build

COPY TelegramBot/ /app/

WORKDIR /app/

RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine

COPY --from=build /app/out /app/TelegramBot/

COPY scrapers/ /app/scrapers/

COPY requirements.txt /app/

WORKDIR /app/

RUN apk add --no-cache python3 py3-pip

RUN python3 -m venv /venv && \
    . /venv/bin/activate && \
    pip3 install --upgrade pip && \
    pip3 install -r requirements.txt

RUN echo "0 * * * * . /venv/bin/activate && python /app/scrapers/nekretnine-scraper.py && /app/TelegramBot/TelegramBot" > /etc/crontabs/root

CMD ["crond", "-f"]
