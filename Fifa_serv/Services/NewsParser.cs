using Fifa_serv.Models;
using HtmlAgilityPack;

namespace Fifa_serv.Services;

public sealed class NewsParser
{
    public const string SourceUrl = "https://mfkgazprom-ugra.ru/news/";
    private readonly HttpClient _http;

    public NewsParser(HttpClient http) => _http = http;

    public async Task<List<News>> ParseAsync(CancellationToken cancellationToken = default)
    {
        var html = await _http.GetStringAsync(SourceUrl, cancellationToken);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var links = doc.DocumentNode.SelectNodes("//a[contains(@href, '/news/') and normalize-space(.) != '']")?.AsEnumerable() ?? Enumerable.Empty<HtmlNode>();
        var urls = links.Select(x => Absolute(x.GetAttributeValue("href", string.Empty)))
            .Where(x => x != SourceUrl && !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();

        var result = new List<News>();
        foreach (var url in urls)
        {
            var item = await ParseArticleAsync(url, cancellationToken);
            if (item is not null) result.Add(item);
        }

        if (result.Count == 0)
            throw new InvalidOperationException("Не удалось распознать новости. Возможно, изменилась HTML-разметка источника.");

        return result;
    }

    private async Task<News?> ParseArticleAsync(string url, CancellationToken cancellationToken)
    {
        var html = await _http.GetStringAsync(url, cancellationToken);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var title = FirstText(doc, "//h1", "//meta[@property='og:title']/@content");
        if (string.IsNullOrWhiteSpace(title)) return null;

        var date = FirstText(doc, "//time", "//*[contains(@class,'date')]");
        var contentNode = doc.DocumentNode.SelectSingleNode("//article")
            ?? doc.DocumentNode.SelectSingleNode("//*[contains(@class,'news-detail')]")
            ?? doc.DocumentNode.SelectSingleNode("//*[contains(@class,'content')]");
        var text = contentNode is null ? string.Empty : Clean(contentNode.InnerText);
        var image = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']")?.GetAttributeValue("content", string.Empty)
            ?? contentNode?.SelectSingleNode(".//img")?.GetAttributeValue("src", string.Empty)
            ?? string.Empty;

        var news = new News
        {
            Id = StableId(url),
            Title = title,
            Date = date,
            Text = text,
            ImageUrl = Absolute(image)
        };
        news.Hash = EntityHashFactory.Create(new { news.Title, news.Date, news.Text, news.ImageUrl, Url = url });
        return news;
    }

    private static string FirstText(HtmlDocument doc, params string[] xpaths)
    {
        foreach (var xpath in xpaths)
        {
            var node = doc.DocumentNode.SelectSingleNode(xpath);
            if (node is null) continue;
            var value = node.NodeType == HtmlNodeType.Text ? node.InnerText : node.GetAttributeValue("content", node.InnerText);
            value = Clean(value);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return string.Empty;
    }

    private static string Clean(string value) => string.Join(' ', HtmlEntity.DeEntitize(value).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string Absolute(string url) => string.IsNullOrWhiteSpace(url) ? string.Empty : new Uri(new Uri(SourceUrl), url).ToString();
    private static int StableId(string value) => BitConverter.ToInt32(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)), 0) & int.MaxValue;
}
