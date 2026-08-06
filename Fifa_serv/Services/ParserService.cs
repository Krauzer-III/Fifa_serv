using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using LiteDB;
using Fifa_serv.Models;
using Fifa_serv.Data;

namespace Fifa_serv.Services;

public class ParserService
{
    private readonly HttpClient _httpClient;
    private readonly LiteDbContext _db;
    private readonly HashService _hashService;

    public ParserService(LiteDbContext db, HashService hashService)
    {
        _db = db;
        _hashService = hashService;

        // Настраиваем HttpClient с правильным User-Agent
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    // Главный метод: парсим всю команду
    public async Task<int> ParseTeamAsync()
    {
        const string teamUrl =
            "https://superliga.rfs.ru/tournament/1054805/teams/application?team_id=1258505";

        using var response = await _httpClient.GetAsync(teamUrl);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var rows = doc.DocumentNode.SelectNodes(
            "//table[contains(@class, 'table--team')]//tbody/tr"
        );

        if (rows == null || rows.Count == 0)
        {
            throw new InvalidOperationException(
                "На странице команды не найдены строки игроков. " +
                "Вероятно, изменилась HTML-разметка сайта."
            );
        }

        var processed = 0;

        foreach (var row in rows)
        {
            if (await ParsePlayerRow(row))
            {
                processed++;
            }
        }

        return processed;
    }

    // Парсим одну строку таблицы
    private async Task<bool> ParsePlayerRow(HtmlNode row)
    {
        try
        {
            var numberText = row.SelectSingleNode(".//td[1]")?.InnerText.Trim();

            if (!int.TryParse(numberText, out var number))
            {
                return false;
            }

            var playerLink = row.SelectSingleNode(
                ".//a[contains(@class, 'table__player')]"
            );

            if (playerLink == null)
            {
                return false;
            }

            var playerUrl = "https://superliga.rfs.ru" + playerLink.GetAttributeValue("href", "");
            var playerName = playerLink.InnerText.Trim();

            // Амплуа (пример: "Ун..", "Зщ.", "Вр.")
            var positionTd = row.SelectSingleNode(".//td[2]");
            var positionRaw = positionTd?.InnerText.Trim() ?? "";
            var position = ParsePosition(positionRaw);

            Console.WriteLine($"  Обработка: №{number} {playerName} ({position})");
            await Task.Delay(2000); // полсекунды между запросами
            // Парсим детальную страницу игрока
            var playerDetails = await ParsePlayerDetailsAsync(playerUrl);

            // Создаём объект игрока
            var player = new Player
            {
                Number = number,
                Name = playerName,
                FullName = playerDetails.FullName,
                Position = position,
                Age = playerDetails.Age,
                BirthDate = playerDetails.BirthDate,
                Games = playerDetails.Games,
                Goals = playerDetails.Goals,
                Assists = playerDetails.Assists,
                YellowCards = playerDetails.YellowCards,
                RedCards = playerDetails.RedCards,
                GoalsConceded = playerDetails.GoalsConceded,
                PhotoBase64 = playerDetails.PhotoBase64,
                PlayerUrl = playerUrl
            };

            // Сохраняем в базу
            player.Hash = _hashService.ComputeHash(player);
            var existing = _db.Players.FindOne(x => x.Number == number);
            if (existing != null)
            {
                player.Id = existing.Id;
                _db.Players.Update(player);
                Console.WriteLine($"    Обновлён: {playerName}");
            }
            else
            {
                _db.Players.Insert(player);
                Console.WriteLine($"    Добавлен: {playerName}");
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при парсинге строки: {ex}");
            return false;
        }
    }

    // Парсим детальную страницу игрока
    private async Task<PlayerDetails> ParsePlayerDetailsAsync(string url)
    {
        var result = new PlayerDetails();

        try
        {
            var html = await _httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Имя + фамилия
            var nameMain = doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'player-promo__name-main')]");
            if (nameMain != null)
                result.FullName = nameMain.InnerText.Trim();

            // Отчество (если есть)
            var nameMiddle = doc.DocumentNode.SelectSingleNode("//span[contains(@class, 'player-promo__name-middle')]");
            if (nameMiddle != null && !string.IsNullOrWhiteSpace(nameMiddle.InnerText))
                result.FullName += " " + nameMiddle.InnerText.Trim();

            // Фото в base64
            var img = doc.DocumentNode.SelectSingleNode("//img[contains(@class, 'player-promo__img')]");
            if (img != null)
            {
                var imgUrl = img.GetAttributeValue("src", "");
                if (!string.IsNullOrEmpty(imgUrl))
                {
                    if (!imgUrl.StartsWith("http"))
                        imgUrl = "https://superliga.rfs.ru" + imgUrl;

                    result.PhotoBase64 = await DownloadImageAsBase64Async(imgUrl);
                }
            }

            // Статистика: ищем stats-info__main
            var statsBlocks = doc.DocumentNode.SelectNodes("//div[contains(@class, 'stats-info__main')]");
            if (statsBlocks != null)
            {
                foreach (var block in statsBlocks)
                {
                    var label = block.SelectSingleNode(".//div[contains(@class, 'stats-info__text')]")?.InnerText.Trim();
                    var value = block.SelectSingleNode(".//div[contains(@class, 'stats-info__number')]")?.InnerText.Trim();

                    if (string.IsNullOrEmpty(label) || string.IsNullOrEmpty(value)) continue;

                    switch (label)
                    {
                        case "Игры":
                        case "Игр":
                            result.Games = ParseInt(value);
                            break;
                        case "Голы":
                            result.Goals = ParseInt(value);
                            break;
                        case "Передачи":
                            result.Assists = ParseInt(value);
                            break;
                        case "ЖК":
                        case "Жёлтые карточки":
                            result.YellowCards = ParseInt(value);
                            break;
                        case "КК":
                        case "Красные карточки":
                            result.RedCards = ParseInt(value);
                            break;
                        case "Пропущено":
                            result.GoalsConceded = ParseInt(value);
                            break;
                    }
                }
            }

            // Дата рождения и возраст из заголовка страницы
            var birthInfo = ExtractBirthDateAndAge(html);
            result.BirthDate = birthInfo.date;
            result.Age = birthInfo.age;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    Ошибка парсинга страницы {url}: {ex.Message}");
        }

        return result;
    }

    // Вспомогательный метод: загрузка фото и конвертация в base64
    private async Task<string> DownloadImageAsBase64Async(string url)
    {
        try
        {
            var bytes = await _httpClient.GetByteArrayAsync(url);
            var base64 = Convert.ToBase64String(bytes);

            // Определяем тип контента по расширению
            var ext = Path.GetExtension(url).ToLower();
            var mime = ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };

            return $"data:{mime};base64,{base64}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"      Ошибка загрузки фото {url}: {ex.Message}");
            return "";
        }
    }

    // Парсим амплуа из сокращения
    private string ParsePosition(string raw)
    {
        return raw switch
        {
            "Вр." => "Вратарь",
            "Зщ." => "Защитник",
            "Нп." => "Нападающий",
            "Ун.." or "Ун." => "Универсал",
            _ => raw
        };
    }

    // Безопасный парсинг int
    private int ParseInt(string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        var cleaned = Regex.Match(value, @"\d+").Value;
        return int.TryParse(cleaned, out var result) ? result : 0;
    }

    // Извлечение даты рождения и возраста из HTML
    private (string date, int? age) ExtractBirthDateAndAge(string html)
    {
        // Ищем шаблон: 22.09.2000, 25 лет
        var match = Regex.Match(html, @"(\d{2})\.(\d{2})\.(\d{4}),\s*(\d+)\s*лет");
        if (match.Success)
        {
            var date = $"{match.Groups[3].Value}-{match.Groups[2].Value}-{match.Groups[1].Value}";
            var age = int.Parse(match.Groups[4].Value);
            return (date, age);
        }
        return ("", null);
    }

    // Вспомогательный класс для сбора данных
    private class PlayerDetails
    {
        public string FullName { get; set; } = "";
        public string BirthDate { get; set; } = "";
        public int? Age { get; set; }
        public int Games { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int YellowCards { get; set; }
        public int RedCards { get; set; }
        public int? GoalsConceded { get; set; }
        public string PhotoBase64 { get; set; } = "";
    }

    public async Task ParseMatchesAsync(string link, string matchType = "Регулярный")
    {
        var calendarUrl = link;

        Console.WriteLine("Начинаем парсинг всех матчей...");

        var html = await _httpClient.GetStringAsync(calendarUrl);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Находим все блоки с датами
        var dateBlocks = doc.DocumentNode.SelectNodes("//div[contains(@class, 'timetable__unit')]");
        if (dateBlocks == null || dateBlocks.Count == 0)
        {
            Console.WriteLine("Не найдены блоки с датами");
            return;
        }

        Console.WriteLine($"Найдено блоков с датами: {dateBlocks.Count}");

        foreach (var dateBlock in dateBlocks)
        {
            // Извлекаем дату
            var dateHeader = dateBlock.SelectSingleNode(".//span[contains(@class, 'timetable__head-text')]");
            if (dateHeader == null) continue;

            var dateText = dateHeader.InnerText.Trim();
            var dateMatch = Regex.Match(dateText, @"(\d+)\s+(\w+)\s+(\d+),\s+(\w+)");
            if (!dateMatch.Success) continue;

            var day = dateMatch.Groups[1].Value.PadLeft(2, '0');
            var month = ParseMonth(dateMatch.Groups[2].Value);
            var year = dateMatch.Groups[3].Value;
            var weekday = dateMatch.Groups[4].Value;
            var formattedDate = $"{year}-{month}-{day}";

            Console.WriteLine($"\n📅 {dateText} -> {formattedDate}");

            // Находим все матчи в блоке
            var matchNodes = dateBlock.SelectNodes(".//li[contains(@class, 'timetable__item')]");
            if (matchNodes == null) continue;

            foreach (var matchNode in matchNodes)
            {
                await ParseMatchSimple(matchNode, formattedDate, weekday, matchType);
            }
        }

        Console.WriteLine("\n✅ Парсинг завершён!");
    }

    private async Task ParseMatchSimple(HtmlNode matchNode, string date, string weekday, string matchType)
    {
        try
        {
            // 1. Находим названия команд (берём ВСЕ подряд)
            var teamNames = new List<string>();
            var teamLogos = new List<string>();

            var teamElements = matchNode.SelectNodes(".//a[contains(@class, 'timetable__team')]");
            if (teamElements != null)
            {
                foreach (var team in teamElements)
                {
                    var nameNode = team.SelectSingleNode(".//div[contains(@class, 'timetable__team-name')]");
                    teamNames.Add(nameNode?.InnerText.Trim() ?? "Неизвестно");

                    // Логотип
                    var logoNode = team.SelectSingleNode(".//img");
                    if (logoNode != null)
                    {
                        var logoUrl = logoNode.GetAttributeValue("src", "");
                        if (!string.IsNullOrEmpty(logoUrl) && logoUrl.StartsWith("/"))
                            logoUrl = "https://superliga.rfs.ru" + logoUrl;
                        teamLogos.Add(!string.IsNullOrEmpty(logoUrl) ? await DownloadImageAsBase64Async(logoUrl) : "");
                    }
                    else
                    {
                        teamLogos.Add("");
                    }
                }
            }

            // Если нашли меньше 2 команд — пропускаем
            if (teamNames.Count < 2)
            {
                Console.WriteLine($"  ⚠️ Пропущен матч: найдено команд {teamNames.Count}");
                return;
            }

            // 2. Счёт
            var scoreNode = matchNode.SelectSingleNode(".//div[contains(@class, 'timetable__score-main')]");
            var score = scoreNode?.InnerText.Trim() ?? "-:-";

            // 3. Ссылка на матч
            var matchLink = matchNode.SelectSingleNode(".//a[contains(@class, 'timetable__score')]");
            var matchUrl = matchLink != null ? "https://superliga.rfs.ru" + matchLink.GetAttributeValue("href", "") : "";

            // 4. Время
            var timeNode = matchNode.SelectSingleNode(".//span[contains(@class, 'timetable__time')]");
            var time = timeNode?.InnerText.Trim() ?? "";

            // 5. Стадион
            var placeNode = matchNode.SelectSingleNode(".//div[contains(@class, 'timetable__place')]");
            string stadium = "";
            string city = "";
            if (placeNode != null)
            {
                stadium = placeNode.SelectSingleNode(".//span[contains(@class, 'timetable__place-name')]")?.InnerText.Trim() ?? "";
                var placeText = placeNode.GetAttributeValue("title", "") ?? stadium;
                city = ExtractCity(placeText);
            }

            // 6. Тур
            var roundNode = matchNode.SelectSingleNode(".//span[contains(@class, 'timetable__round')]");
            var roundText = roundNode?.InnerText.Trim() ?? "";
            var round = ParseInt(Regex.Match(roundText, @"\d+").Value);

            // Сохраняем
            var match = new Models.Match
            {
                Round = round,
                Team1 = teamNames[0],
                Team2 = teamNames[1],
                Score = score,
                Date = date,
                Weekday = weekday,
                Time = time,
                City = city,
                Stadium = stadium,
                IsHome = false, // Не определяем, пусть будет false
                Status = score == "-:-" ? "upcoming" : "finished",
                MatchUrl = matchUrl,
                Team1Logo = teamLogos.Count > 0 ? teamLogos[0] : "",
                Team2Logo = teamLogos.Count > 1 ? teamLogos[1] : "",
                MatchType = matchType
            };

            match.Hash = _hashService.ComputeHash(match);

            // Сохраняем
            var existing = _db.Matches.FindOne(x => x.MatchUrl == matchUrl);
            if (existing != null)
            {
                // Вычисляем хеш новых данных
                match.Hash = _hashService.ComputeHash(match);

                // Сравниваем хеши
                if (existing.Hash == match.Hash)
                {
                    Console.WriteLine($"  ⏭️ Без изменений: {match.Team1} vs {match.Team2} ({score})");
                    return; // Хеш совпадает, ничего не делаем
                }

                // Хеш изменился — обновляем
                match.Id = existing.Id;
                _db.Matches.Update(match);
                Console.WriteLine($"  🔄 Обновлён: {match.Team1} vs {match.Team2} ({score}) (хеш изменился)");
            }
            else
            {
                _db.Matches.Insert(match);
                Console.WriteLine($"  ✅ Добавлен: {match.Team1} vs {match.Team2} ({score})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Ошибка: {ex.Message}");
        }
    }

    private async Task ParseSingleMatch(HtmlNode matchNode, string currentDate, string currentWeekday)
    {
        try
        {
            // 1. Ищем все команды в матче
            var teamElements = matchNode.SelectNodes(".//a[contains(@class, 'timetable__team')]");
            if (teamElements == null || teamElements.Count < 2)
            {
                Console.WriteLine("  Не найдены команды в матче");
                return;
            }

            string team1Name = null;
            string team2Name = null;
            string team1Logo = null;
            string team2Logo = null;
            int? team1Id = null;
            int? team2Id = null;
            bool isHome = false;

            foreach (var teamElement in teamElements)
            {
                // Извлекаем ID команды из href
                var href = teamElement.GetAttributeValue("href", "");
                var teamIdMatch = Regex.Match(href, @"team_id=(\d+)");
                var teamId = teamIdMatch.Success ? int.Parse(teamIdMatch.Groups[1].Value) : (int?)null;

                // Название команды
                var nameNode = teamElement.SelectSingleNode(".//div[contains(@class, 'timetable__team-name')]");
                var teamName = nameNode?.InnerText.Trim() ?? "";

                // Логотип команды
                string logoUrl = null;
                var logoNode = teamElement.SelectSingleNode(".//img[contains(@class, 'timetable__team-img')]");
                if (logoNode != null)
                {
                    logoUrl = logoNode.GetAttributeValue("src", "");
                    if (!string.IsNullOrEmpty(logoUrl) && logoUrl.StartsWith("/"))
                        logoUrl = "https://superliga.rfs.ru" + logoUrl;
                }

                // Определяем, наш ли это клуб (Газпром-Югра)
                if (teamId == 1258505)
                {
                    // Проверяем, какая позиция у нашего клуба (первая или вторая в DOM)
                    // Если наш клуб первый в списке teams -> домашний матч
                    isHome = teamElements.IndexOf(teamElement) == 0;
                    continue;
                }

                // Заполняем данные соперника
                if (team1Id == null)
                {
                    team1Name = teamName;
                    team1Logo = logoUrl;
                    team1Id = teamId;
                }
                else
                {
                    team2Name = teamName;
                    team2Logo = logoUrl;
                    team2Id = teamId;
                }
            }

            // Если не нашли соперника, выходим
            if (team1Id == null && team2Id == null)
            {
                Console.WriteLine("  Не найден соперник для Газпром-Югра");
                return;
            }

            // 2. Счёт матча
            var scoreNode = matchNode.SelectSingleNode(".//div[contains(@class, 'timetable__score-main')]");
            var score = scoreNode?.InnerText.Trim() ?? "-:-";

            // 3. Ссылка на страницу матча
            var matchLink = matchNode.SelectSingleNode(".//a[contains(@class, 'timetable__score')]");
            var matchUrl = matchLink != null ? "https://superliga.rfs.ru" + matchLink.GetAttributeValue("href", "") : "";

            // 4. Время матча
            var timeNode = matchNode.SelectSingleNode(".//span[contains(@class, 'timetable__time')]");
            var time = timeNode?.InnerText.Trim() ?? "";

            // 5. Место проведения (стадион)
            var placeNode = matchNode.SelectSingleNode(".//div[contains(@class, 'timetable__place')]");
            string stadium = "";
            string stadiumFull = "";
            string city = "";

            if (placeNode != null)
            {
                stadium = placeNode.SelectSingleNode(".//span[contains(@class, 'timetable__place-name')]")?.InnerText.Trim() ?? "";
                stadiumFull = placeNode.GetAttributeValue("title", "") ?? stadium;
                city = ExtractCity(stadiumFull);
            }

            // 6. Номер тура
            var roundNode = matchNode.SelectSingleNode(".//span[contains(@class, 'timetable__round')]");
            var roundText = roundNode?.InnerText.Trim() ?? "";
            var round = ParseInt(Regex.Match(roundText, @"\d+").Value);

            // 7. Дата и день недели (передаются из метода ParseMatchesAsync)
            var date = currentDate;
            var weekday = currentWeekday;
            var status = score == "-:-" ? "upcoming" : "finished";

            Console.WriteLine($"  Матч: {team1Name} vs {team2Name}, счёт: {score}, дом/выезд: {(isHome ? "дома" : "выезд")}, тур: {round}, дата: {date}");

            // 8. Создаём объект матча
            var match = new Models.Match  // используйте GameMatch или MatchModel
            {
                Round = round,
                Team1 = team1Name ?? "Неизвестно",
                Team2 = team2Name ?? "Неизвестно",
                Score = score,
                Date = date,
                Weekday = weekday,
                Time = time,
                City = city,
                Stadium = stadium,
                StadiumAddress = stadiumFull,
                IsHome = isHome,
                Status = status,
                MatchUrl = matchUrl,
                Team1Logo = !string.IsNullOrEmpty(team1Logo) ? await DownloadImageAsBase64Async(team1Logo) : "",
                Team2Logo = !string.IsNullOrEmpty(team2Logo) ? await DownloadImageAsBase64Async(team2Logo) : ""
            };

            // 9. Вычисляем хеш и сохраняем в БД
            match.Hash = _hashService.ComputeHash(match);

            var existing = _db.Matches.FindOne(x => x.MatchUrl == matchUrl);
            if (existing != null)
            {
                match.Id = existing.Id;
                _db.Matches.Update(match);
                Console.WriteLine($"    Обновлён матч: {match.Team1} vs {match.Team2}");
            }
            else
            {
                _db.Matches.Insert(match);
                Console.WriteLine($"    Добавлен матч: {match.Team1} vs {match.Team2}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Ошибка при парсинге матча: {ex.Message}");
        }
    }

    private string ExtractCity(string stadiumInfo)
    {
        if (string.IsNullOrEmpty(stadiumInfo)) return "";

        
        var match = Regex.Match(stadiumInfo, @",\s*([^,]+?)(?:,|$)");
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private string ParseMonth(string monthName)
    {
        return monthName.ToLower() switch
        {
            "января" => "01",
            "февраля" => "02",
            "марта" => "03",
            "апреля" => "04",
            "мая" => "05",
            "июня" => "06",
            "июля" => "07",
            "августа" => "08",
            "сентября" => "09",
            "октября" => "10",
            "ноября" => "11",
            "декабря" => "12",
            _ => "01"
        };
    }

    public async Task<int> ParseTeamRatingsAsync(
    string link,
    string ratingType = "Основной")
    {
        Console.WriteLine(
            $"Начинаем парсинг рейтинга: {ratingType}");

        var html = await _httpClient.GetStringAsync(link);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var rows = doc.DocumentNode.SelectNodes(
            "//ul[contains(concat(' ', normalize-space(@class), ' '), " +
            "' custom-table__body ')]" +
            "/li[contains(concat(' ', normalize-space(@class), ' '), " +
            "' custom-table__line ')]"
        );

        if (rows == null || rows.Count == 0)
        {
            throw new InvalidOperationException(
                $"Не найдены строки рейтинга: {ratingType}");
        }

        var processed = 0;

        foreach (var row in rows)
        {
            try
            {
                var positionNode = row.SelectSingleNode(
                    ".//div[contains(@class, " +
                    "'custom-table__number-wrapper')]"
                );

                var position = ParseInt(
                    CleanRatingText(positionNode));

                var teamLink = row.SelectSingleNode(
                    ".//a[contains(concat(' ', normalize-space(@class), ' '), " +
                    "' custom-table__team ')]"
                );

                var teamNameNode = row.SelectSingleNode(
                    ".//div[contains(concat(' ', normalize-space(@class), ' '), " +
                    "' custom-table__team-name ')]"
                );

                if (position <= 0 ||
                    teamLink == null ||
                    teamNameNode == null)
                {
                    continue;
                }

                var relativeTeamUrl = teamLink.GetAttributeValue(
                    "href",
                    string.Empty);

                var teamUrl = MakeRatingAbsoluteUrl(relativeTeamUrl);
                var teamId = ExtractRatingTeamId(relativeTeamUrl);
                var teamName = CleanRatingText(teamNameNode);

                var logoNode = teamLink.SelectSingleNode(
                    ".//img[contains(concat(' ', normalize-space(@class), ' '), " +
                    "' custom-table__team-img ')]"
                );

                var logoUrl = MakeRatingAbsoluteUrl(
                    logoNode?.GetAttributeValue("src", string.Empty)
                    ?? string.Empty);

                var logoBase64 = string.IsNullOrWhiteSpace(logoUrl)
                    ? string.Empty
                    : await DownloadImageAsBase64Async(logoUrl);

                /*
                 * Обычные числовые поля:
                 * 0 — И
                 * 1 — В
                 * 2 — ВП
                 * 3 — ПП
                 * 4 — П
                 * 5 — О
                 */
                var valueNodes = row.SelectNodes(
                    ".//div[" +
                    "contains(concat(' ', normalize-space(@class), ' '), " +
                    "' custom-table__var ') and " +
                    "not(contains(concat(' ', normalize-space(@class), ' '), " +
                    "' custom-table__var--diff '))" +
                    "]" +
                    "/div[contains(concat(' ', normalize-space(@class), ' '), " +
                    "' custom-table__content ')]"
                );

                var values = valueNodes?
                    .Select(node => ParseInt(CleanRatingText(node)))
                    .ToList()
                    ?? new List<int>();

                if (values.Count < 6)
                {
                    Console.WriteLine(
                        $"Пропущена команда {teamName}: " +
                        $"найдено полей {values.Count} вместо 6");

                    continue;
                }

                var goalsNode = row.SelectSingleNode(
                    ".//div[contains(concat(' ', normalize-space(@class), ' '), " +
                    "' custom-table__var--diff ')]" +
                    "//div[contains(concat(' ', normalize-space(@class), ' '), " +
                    "' custom-table__content ')]"
                );

                var goals = ParseRatingGoals(
                    CleanRatingText(goalsNode));

                var rating = new TeamRating
                {
                    Position = position,
                    TeamId = teamId,
                    TeamName = teamName,
                    TeamUrl = teamUrl,
                    LogoBase64 = logoBase64,

                    Games = values[0],
                    Wins = values[1],
                    WinsAfterPenalties = values[2],
                    LossesAfterPenalties = values[3],
                    Losses = values[4],

                    GoalsFor = goals.goalsFor,
                    GoalsAgainst = goals.goalsAgainst,
                    Points = values[5],

                    RatingType = ratingType
                };

                rating.Hash = _hashService.ComputeHash(new
                {
                    rating.Position,
                    rating.TeamId,
                    rating.TeamName,
                    rating.TeamUrl,
                    rating.LogoBase64,
                    rating.Games,
                    rating.Wins,
                    rating.WinsAfterPenalties,
                    rating.LossesAfterPenalties,
                    rating.Losses,
                    rating.GoalsFor,
                    rating.GoalsAgainst,
                    rating.Points,
                    rating.RatingType
                });

                var existing = _db.TeamRatings.FindOne(x =>
                    x.TeamId == rating.TeamId &&
                    x.RatingType == ratingType);

                if (existing == null)
                {
                    _db.TeamRatings.Insert(rating);

                    Console.WriteLine(
                        $"Добавлена: {rating.Position}. " +
                        $"{rating.TeamName} [{ratingType}]");
                }
                else if (existing.Hash != rating.Hash)
                {
                    rating.Id = existing.Id;
                    _db.TeamRatings.Update(rating);

                    Console.WriteLine(
                        $"Обновлена: {rating.Position}. " +
                        $"{rating.TeamName} [{ratingType}]");
                }
                else
                {
                    Console.WriteLine(
                        $"Без изменений: {rating.TeamName} [{ratingType}]");
                }

                processed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Ошибка обработки строки рейтинга: {ex.Message}");
            }
        }

        if (processed == 0)
        {
            throw new InvalidOperationException(
                $"Не обработано ни одной команды: {ratingType}");
        }

        Console.WriteLine(
            $"Рейтинг {ratingType} обработан: {processed} команд");

        return processed;
    }
    private static string CleanRatingText(HtmlNode? node)
    {
        if (node == null)
        {
            return string.Empty;
        }

        return HtmlEntity
            .DeEntitize(node.InnerText)
            .Replace('\u00A0', ' ')
            .Trim();
    }

    private static string MakeRatingAbsoluteUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        return new Uri(
            new Uri("https://superliga.rfs.ru"),
            url
        ).ToString();
    }

    private static int? ExtractRatingTeamId(string url)
    {
        var match = Regex.Match(
            url ?? string.Empty,
            @"(?:\?|&)team_id=(\d+)",
            RegexOptions.IgnoreCase);

        return match.Success &&
               int.TryParse(match.Groups[1].Value, out var teamId)
            ? teamId
            : null;
    }

    private static (int goalsFor, int goalsAgainst)
        ParseRatingGoals(string text)
    {
        var match = Regex.Match(
            text ?? string.Empty,
            @"(\d+)\s*[-–—:]\s*(\d+)");

        if (!match.Success)
        {
            return (0, 0);
        }

        var goalsFor = int.TryParse(
            match.Groups[1].Value,
            out var parsedFor)
                ? parsedFor
                : 0;

        var goalsAgainst = int.TryParse(
            match.Groups[2].Value,
            out var parsedAgainst)
                ? parsedAgainst
                : 0;

        return (goalsFor, goalsAgainst);
    }


    public async Task<int> ParseNewsAsync(
    string link = "https://mfkgazprom-ugra.ru/news/")
    {
        Console.WriteLine("Начинаем парсинг новостей...");

        var html = await _httpClient.GetStringAsync(link);

        var document = new HtmlDocument();
        document.LoadHtml(html);

        var newsNodes = document.DocumentNode.SelectNodes(
            "//div[" +
            "contains(concat(' ', normalize-space(@class), ' '), ' post ') and " +
            "contains(concat(' ', normalize-space(@class), ' '), ' post-type-1 ')" +
            "]"
        );

        if (newsNodes == null || newsNodes.Count == 0)
        {
            throw new InvalidOperationException(
                "Не найдены блоки новостей div.post.post-type-1. " +
                "Возможно, изменилась HTML-разметка сайта."
            );
        }

        var processed = 0;

        foreach (var newsNode in newsNodes)
        {
            try
            {
                var saved = await ParseNewsNodeAsync(newsNode);

                if (saved)
                {
                    processed++;
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    $"Ошибка обработки новости: {exception.Message}"
                );
            }
        }

        if (processed == 0)
        {
            throw new InvalidOperationException(
                "Блоки новостей найдены, но ни одну новость " +
                "не удалось обработать."
            );
        }

        Console.WriteLine(
            $"Парсинг новостей завершён. Обработано: {processed}"
        );

        return processed;
    }

    private async Task<bool> ParseNewsNodeAsync(HtmlNode newsNode)
    {
        var linkNode = newsNode.SelectSingleNode(".//a[@href]");

        if (linkNode == null)
        {
            Console.WriteLine(
                "Пропущена новость: не найдена ссылка."
            );

            return false;
        }

        var relativeNewsUrl = linkNode.GetAttributeValue(
            "href",
            string.Empty
        );

        var newsUrl = MakeNewsAbsoluteUrl(relativeNewsUrl);

        if (string.IsNullOrWhiteSpace(newsUrl))
        {
            Console.WriteLine(
                "Пропущена новость: ссылка пустая."
            );

            return false;
        }

        var titleNode = newsNode.SelectSingleNode(
            ".//div[" +
            "contains(concat(' ', normalize-space(@class), ' '), ' text ')" +
            "]/p"
        );

        var title = CleanNewsText(titleNode);

        if (string.IsNullOrWhiteSpace(title))
        {
            Console.WriteLine(
                $"Пропущена новость {newsUrl}: заголовок пустой."
            );

            return false;
        }

        var categoryNode = newsNode.SelectSingleNode(
            ".//div[" +
            "contains(concat(' ', normalize-space(@class), ' '), ' category ')" +
            "]" +
            "//div[" +
            "contains(concat(' ', normalize-space(@class), ' '), ' td ')" +
            "]"
        );

        var category = CleanNewsText(categoryNode);

        var imageUrl = ExtractNewsImageUrl(newsNode);
        imageUrl = MakeNewsAbsoluteUrl(imageUrl);

        var imageBase64 = string.IsNullOrWhiteSpace(imageUrl)
            ? string.Empty
            : await DownloadImageAsBase64Async(imageUrl);

        var news = new News
        {
            Title = title,
            Category = category,
            NewsUrl = newsUrl,
            ImageBase64 = imageBase64
        };

        news.Hash = _hashService.ComputeHash(news);

        var existing = _db.News.FindOne(
            x => x.NewsUrl == news.NewsUrl
        );

        if (existing == null)
        {
            _db.News.Insert(news);

            Console.WriteLine(
                $"Добавлена новость: {news.Title}"
            );

            return true;
        }

        if (existing.Hash == news.Hash)
        {
            Console.WriteLine(
                $"Без изменений: {news.Title}"
            );

            return true;
        }

        news.Id = existing.Id;
        _db.News.Update(news);

        Console.WriteLine(
            $"Обновлена новость: {news.Title}"
        );

        return true;
    }

    private static string ExtractNewsImageUrl(HtmlNode newsNode)
    {
        var style = newsNode.GetAttributeValue(
            "style",
            string.Empty
        );

        if (string.IsNullOrWhiteSpace(style))
        {
            return string.Empty;
        }

        style = HtmlEntity.DeEntitize(style);

        var match = Regex.Match(
            style,
            @"url\(\s*['""]?(?<url>[^'"")]+)['""]?\s*\)",
            RegexOptions.IgnoreCase
        );

        return match.Success
            ? match.Groups["url"].Value.Trim()
            : string.Empty;
    }

    private static string CleanNewsText(HtmlNode? node)
    {
        if (node == null)
        {
            return string.Empty;
        }

        return HtmlEntity
            .DeEntitize(node.InnerText)
            .Replace('\u00A0', ' ')
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ")
            .Trim();
    }

    private static string MakeNewsAbsoluteUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(
            url,
            UriKind.Absolute,
            out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        return new Uri(
            new Uri("https://mfkgazprom-ugra.ru/"),
            url
        ).ToString();
    }
}