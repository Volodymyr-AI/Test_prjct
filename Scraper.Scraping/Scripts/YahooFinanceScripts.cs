namespace Scraper.Scraping.Scripts;

public static class YahooFinanceScripts
{
    /// <summary>
    /// Extracts news items from Yahoo Finance stream DOM.
    /// Returns JSON string to avoid Playwright type converter issues.
    /// </summary>
    public const string ExtractArticles = """
                              (() => {
                                  const items = [];
                                  const seen  = new Set();

                                  document.querySelectorAll('[data-testid="storyitem"]').forEach(el => {
                                      // URL + Title
                                      const titleAnchor = el.querySelector('a.titles, a[class*="titles"]');
                                      const h3          = el.querySelector('h3');

                                      if (!titleAnchor || !h3) return;

                                      const url   = titleAnchor.href;
                                      const title = h3.innerText?.trim();

                                      if (!url || !title || seen.has(url)) return;
                                      seen.add(url);

                                      // "Associated Press Finance · 29m ago" або "Reuters · 2h ago"
                                      const publishing = el.querySelector('div.publishing, [class*="publishing"]');
                                      let source      = null;
                                      let publishedAt = null;

                                      if (publishing) {
                                          const text = publishing.innerText?.trim() ?? '';
                                          const parts = text.split(/[·•|]/);
                                          if (parts.length >= 2) {
                                              source      = parts[0].trim();
                                              const timeStr = parts[parts.length - 1].trim();
                                              publishedAt = timeStr;
                                          } else {
                                              source = text;
                                          }
                                      }

                                      items.push({
                                          url,
                                          title,
                                          summary:     '',
                                          source:      source ?? '',
                                          publishedAt: publishedAt ?? ''
                                      });
                                  });

                                  return JSON.stringify(items);
                              })()
                              """;
    
    /// <summary>
    /// Returns count of currently loaded stream items.
    /// Used during scroll loop to detect new content.
    /// </summary>
    public const string CountStreamItems = """
                                           document.querySelectorAll('li[class*="stream-item"],div[class*="stream-item"],[data-testid="storyitem"]').length
                                           """;
}