# Scalability Roadmap

This document describes how the scraper architecture can evolve to support multiple sites and higher load. The current implementation is intentionally focused on a single source (Yahoo Finance) — the roadmap below outlines a natural progression from the existing codebase.

---

## Current State

```
ScraperWorker
    └─ ScrapingOrchestrator
         └─ PlaywrightScraperService   ← tightly coupled to Yahoo Finance
              ├─ CSS selectors specific to Yahoo Finance DOM
              ├─ Relative time parser ("29m ago", "2h ago")
              └─ XHR interception pattern for Yahoo internal API
```

Works well for one source. Adding a second site would require duplicating or forking `PlaywrightScraperService`.

---

## Phase 1 — Multi-Site Support (minimal changes)

Introduce a `ISiteParser` abstraction inside `Scraper.Core`. Each site gets its own parser implementation in `Scraper.Scraping`. `PlaywrightScraperService` becomes a browser coordinator — it opens the browser and delegates DOM work to the correct parser.

```
Scraper.Core
    └─ Interfaces/
         └─ ISiteParser.cs              ← new interface

Scraper.Scraping
    └─ Parsers/
         ├─ YahooFinanceParser.cs       ← extracted from PlaywrightScraperService
         ├─ BloombergParser.cs          ← new
         └─ ReutersParser.cs            ← new
```

**`ISiteParser` contract:**

```csharp
public interface ISiteParser
{
    string SiteName { get; }
    bool CanHandle(string url);
    Task<IReadOnlyList<ScrapedItem>> ParseAsync(IPage page, CancellationToken ct);
}
```

**`PlaywrightScraperService` becomes a coordinator:**

```csharp
// Selects the correct parser by URL, opens browser, delegates parsing
var parser = _parsers.First(p => p.CanHandle(targetUrl));
var items  = await parser.ParseAsync(page, ct);
```

**Registration stays clean:**

```csharp
services.AddPlaywrightScraper(configuration);   // registers all parsers automatically
```

**What this solves:** each site is isolated. Adding a new source means adding one class — nothing else changes.

**What this does not solve:** sites are still scraped sequentially, one after another.

---

## Phase 2 — Parallel Scraping

With 10 sites at ~10 seconds each, sequential execution takes ~100 seconds per run. Running in parallel brings this down to ~10 seconds.

```csharp
var tasks = sites.Select(site => orchestrator.RunAsync(site, ct));
await Task.WhenAll(tasks);
```

**Memory constraint:** each Playwright Chromium instance uses ~150–200 MB RAM. Ten browsers simultaneously = ~2 GB. This is controlled with a semaphore:

```csharp
// Maximum 3 browsers running concurrently
private readonly SemaphoreSlim _semaphore = new(3);

await _semaphore.WaitAsync(ct);
try   { await orchestrator.RunAsync(site, ct); }
finally { _semaphore.Release(); }
```

The concurrency limit is configurable in `appsettings.json`:

```json
{
  "Worker": {
    "MaxConcurrentBrowsers": 3
  }
}
```

---

## Phase 3 — Distributed Architecture (high load)

When a single process is no longer enough — multiple machines, dozens of sources, independent scaling of scraping vs. storage.

```
┌─────────────┐     ┌──────────────────────┐     ┌─────────────────────┐
│  Scheduler  │────▶│  Message Queue       │────▶│  Scraper Workers    │
│  (cron/k8s) │     │  (Kafka / RabbitMQ)  │     │  (N instances)      │
└─────────────┘     └──────────────────────┘     └──────────┬──────────┘
                                                            │
                                              ┌─────────────▼─────────────┐
                                              │  ClickHouse  │  Redis      │
                                              │  (shared)    │  (shared)   │
                                              └───────────────────────────┘
```

- **Scheduler** publishes scraping tasks: `{ "site": "yahoo", "url": "..." }`
- **Workers** are stateless — any worker picks any task from the queue
- **Redis** deduplication still works across all workers (shared set)
- **ClickHouse** `ReplacingMergeTree` handles any remaining duplicates at the DB level
- Workers scale horizontally — add more containers under high load, remove them when idle

**Why the current architecture already supports this:** `IArticleRepository`, `IUrlCache`, and `IScraperService` are interfaces. Swapping from a single-process model to a distributed one means changing `Program.cs` and adding a queue consumer — the domain logic and infrastructure implementations stay the same.

---

## Summary

| Phase | Effort | Addresses |
|-------|--------|-----------|
| Phase 1 — `ISiteParser` abstraction | Low | Multiple sites without code duplication |
| Phase 2 — `Task.WhenAll` + `SemaphoreSlim` | Low | Parallel scraping, controlled RAM usage |
| Phase 3 — Message queue + distributed workers | High | Horizontal scaling, independent deployments |

The progression is intentional: start simple, add complexity only when the bottleneck is proven. The current Onion Architecture makes each phase addable without rewriting the existing layers.