# Softoria Scraper

Yahoo Finance Latest News scraper built with **.NET 10 Worker Service**, **Playwright**, **ClickHouse**, and **Redis**.

## Architecture
```
Scraper.Core          — domain entities, interfaces (zero dependencies)
Scraper.Persistence   — ClickHouse repository (IArticleRepository)
Scraper.Redis         — Redis URL cache (IUrlCache)
Scraper.Scraping      — Playwright scraper (IScraperService)
Scraper.Worker        — BackgroundService, orchestration, entry point
```

**Onion dependency flow:**
```
Scraper.Worker → Scraper.Persistence ┐
               → Scraper.Redis       ├→ Scraper.Core (no deps)
               → Scraper.Scraping    ┘
```

## How it works

1. **Playwright** navigates to Yahoo Finance Latest News and scrolls to load all articles
2. **Resource blocking** aborts images, fonts, stylesheets, ad/analytics domains → ~65% bandwidth savings
3. **DOM extraction** parses articles with title, source, published time
4. **Redis** (`SMISMEMBER`) filters already-known URLs in a single round-trip
5. **ClickHouse** bulk-inserts new articles (`ReplacingMergeTree` deduplicates at engine level)
6. **PeriodicTimer** reruns every hour — only new articles are saved

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Quick Start

### 1. Start infrastructure
```bash
docker-compose up -d
```

### 2. Create ClickHouse schema
```bash
docker exec -it softoria_scraper-clickhouse-1 clickhouse-client --query "
CREATE DATABASE IF NOT EXISTS scraper;
CREATE TABLE IF NOT EXISTS scraper.articles
(
    url          String,
    title        String,
    summary      Nullable(String),
    source       Nullable(String),
    published_at Nullable(DateTime64(0, 'UTC')),
    scraped_at   DateTime64(0, 'UTC')
)
ENGINE = ReplacingMergeTree(scraped_at)
ORDER BY url
PARTITION BY toYYYYMM(scraped_at);"
```

### 3. Run the scraper
```bash
dotnet run --project Scraper.Worker
```

Playwright Chromium installs automatically on first run.

### 4. Verify data
```bash
# Article count
docker exec -it softoria_scraper-clickhouse-1 clickhouse-client \
  --query "SELECT count(*) FROM scraper.articles"

# Latest 10 articles
docker exec -it softoria_scraper-clickhouse-1 clickhouse-client \
  --query "SELECT title, source, published_at FROM scraper.articles ORDER BY scraped_at DESC LIMIT 10 FORMAT PrettyCompact"
```

## Configuration

All settings in `Scraper.Worker/appsettings.json`:

| Key | Default | Description |
|-----|---------|-------------|
| `Worker:Interval` | `01:00:00` | How often to scrape |
| `Scraper:MaxScrollIterations` | `60` | Max scroll attempts |
| `Scraper:ScrollDelayMs` | `1500` | Delay between scrolls (ms) |
| `Scraper:StableScrollThreshold` | `3` | Consecutive stable ticks before stop |
| `ConnectionStrings:ClickHouse` | `Host=127.0.0.1;...` | ClickHouse connection |
| `ConnectionStrings:Redis` | `localhost:6379` | Redis connection |

## Traffic optimization

| Technique | Savings |
|-----------|---------|
| Block images, fonts, stylesheets, media | ~65% bandwidth |
| Block ad/analytics domains (Google Analytics, DoubleClick, etc.) | ~10% bandwidth |
| `DOMContentLoaded` instead of `NetworkIdle` | Faster page ready |
| Stable scroll detection — stops when no new content | Avoids unnecessary requests |

## Deduplication

Two-layer deduplication prevents duplicate articles across runs:

- **Redis** — `SMISMEMBER` checks all URLs in one TCP round-trip before any DB write
- **ClickHouse** — `ReplacingMergeTree` deduplicates by `url` asynchronously at engine level

## Expected output

**First run:**
```
Navigating to https://finance.yahoo.com/topic/latest-news/
Scroll complete after 6 iterations (3 consecutive stable ticks)
Merge complete — 170 unique articles (0 from XHR interception, 170 from DOM)
Redis filter: 170 new, 0 already known
Bulk inserted 170 articles into ClickHouse
Run complete in 11.2s — scraped=170, inserted=170, skipped=0
```

**Subsequent runs:**
```
Redis filter: 5 new, 165 already known
Bulk inserted 5 articles into ClickHouse
Run complete in 10.8s — scraped=170, inserted=5, skipped=165
```
