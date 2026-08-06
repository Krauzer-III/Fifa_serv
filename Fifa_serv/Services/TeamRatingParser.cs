using Fifa_serv.Models;
using HtmlAgilityPack;

namespace Fifa_serv.Services;

public sealed class TeamRatingParser
{
    public const string SourceUrl = "https://superliga.rfs.ru/tournament/1054805/stats/teams?common=1";
    private readonly HttpClient _http;

    public TeamRatingParser(HttpClient http) => _http = http;

    public async Task<List<TeamRating>> ParseAsync(CancellationToken cancellationToken = default)
    {
        var html = await _http.GetStringAsync(SourceUrl, cancellationToken);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var rows = doc.DocumentNode.SelectNodes("//table//tbody/tr")?.AsEnumerable() ?? Enumerable.Empty<HtmlNode>();
        var result = new List<TeamRating>();

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("./td");
            if (cells is null || cells.Count < 13) continue;

            var link = cells[1].SelectSingleNode(".//a");
            var rating = new TeamRating
            {
                Position = Int(cells[0]),
                TeamName = Text(link ?? cells[1]),
                TeamUrl = Absolute(link?.GetAttributeValue("href", string.Empty)),
                Games = Int(cells[2]),
                Wins = Int(cells[3]),
                WinsAfterExtraTime = Int(cells[4]),
                WinsAfterPenalties = Int(cells[5]),
                LossesAfterPenalties = Int(cells[6]),
                LossesAfterExtraTime = Int(cells[7]),
                Losses = Int(cells[8]),
                GoalsFor = Int(cells[9]),
                GoalsAgainst = Int(cells[10]),
                YellowCards = Int(cells[11]),
                RedCards = Int(cells[12])
            };
            rating.Id = rating.Position;
            rating.Hash = EntityHashFactory.Create(new { rating.Position, rating.TeamName, rating.TeamUrl, rating.Games, rating.Wins, rating.WinsAfterExtraTime, rating.WinsAfterPenalties, rating.LossesAfterPenalties, rating.LossesAfterExtraTime, rating.Losses, rating.GoalsFor, rating.GoalsAgainst, rating.YellowCards, rating.RedCards });
            result.Add(rating);
        }

        if (result.Count == 0)
            throw new InvalidOperationException("Не удалось найти строки таблицы рейтинга. Возможно, изменилась HTML-разметка источника.");

        return result;
    }

    private static int Int(HtmlNode node) => int.TryParse(Text(node), out var value) ? value : 0;
    private static string Text(HtmlNode node) => HtmlEntity.DeEntitize(node.InnerText).Replace('\u00A0', ' ').Trim();
    private static string Absolute(string url) => string.IsNullOrWhiteSpace(url) ? string.Empty : new Uri(new Uri(SourceUrl), url).ToString();
}
