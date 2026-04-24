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
    public async Task ParseTeamAsync()
    {
        var teamUrl = "https://superliga.rfs.ru/tournament/1054805/teams/application?team_id=1258505";

        Console.WriteLine("Начинаем парсинг команды...");
        var html = await _httpClient.GetStringAsync(teamUrl);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Ищем таблицу с игроками
        var table = doc.DocumentNode.SelectSingleNode("//table[contains(@class, 'table--team')]");
        if (table == null)
        {
            Console.WriteLine("Таблица с игроками не найдена!");
            return;
        }

        var rows = table.SelectNodes(".//tbody//tr");
        if (rows == null)
        {
            Console.WriteLine("Строки с игроками не найдены!");
            return;
        }

        Console.WriteLine($"Найдено {rows.Count} игроков. Начинаем обработку...");

        foreach (var row in rows)
        {
            await ParsePlayerRow(row);
        }

        Console.WriteLine("Парсинг завершён!");
    }

    // Парсим одну строку таблицы
    private async Task ParsePlayerRow(HtmlNode row)
    {
        try
        {
            // Номер игрока
            var numberTd = row.SelectSingleNode(".//td[1]");
            var number = int.Parse(numberTd?.InnerText.Trim() ?? "0");

            // Ссылка на страницу игрока
            var playerLink = row.SelectSingleNode(".//a[contains(@class, 'table__player')]");
            if (playerLink == null) return;

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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при парсинге строки: {ex.Message}");
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

    public async Task ParseMatchesAsync()
    {
        // Базовый URL календаря (можно будет подставить параметры для всех туров)
        var calendarUrl = "https://superliga.rfs.ru/tournament/1054805/calendar";

        Console.WriteLine("Начинаем парсинг матчей...");

        // Загружаем главную страницу календаря
        var html = await _httpClient.GetStringAsync(calendarUrl);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Ищем все элементы матчей
        var matchNodes = doc.DocumentNode.SelectNodes("//li[contains(@class, 'timetable__item')]");

        if (matchNodes == null || matchNodes.Count == 0)
        {
            Console.WriteLine("Матчи не найдены. Возможно, сайт использует динамическую загрузку.");
            return;
        }

        Console.WriteLine($"Найдено {matchNodes.Count} матчей. Фильтруем по участию Газпром-Югра...");

        foreach (var node in matchNodes)
        {
            await ParseSingleMatch(node);
        }

        Console.WriteLine("Парсинг матчей завершён!");
    }

    private async Task ParseSingleMatch(HtmlNode matchNode)
    {
        try
        {
            // Проверяем, участвует ли Газпром-Югра
            var teamLinks = matchNode.SelectNodes(".//a[contains(@class, 'timetable__team')]");
            if (teamLinks == null || teamLinks.Count < 2) return;

            string team1Name = null;
            string team2Name = null;
            string team1Logo = null;
            string team2Logo = null;
            int? team1Id = null;
            int? team2Id = null;

            foreach (var teamLink in teamLinks)
            {
                var nameNode = teamLink.SelectSingleNode(".//div[contains(@class, 'timetable__team-name')]");
                var logoNode = teamLink.SelectSingleNode(".//img[contains(@class, 'timetable__team-img')]");
                var href = teamLink.GetAttributeValue("href", "");

                if (nameNode == null) continue;

                var teamName = nameNode.InnerText.Trim();
                var logoUrl = logoNode?.GetAttributeValue("src", "") ?? "";

                // Извлекаем ID команды из href (например, /tournament/.../application?team_id=1258505)
                var teamIdMatch = Regex.Match(href, @"team_id=(\d+)");
                var teamId = teamIdMatch.Success ? int.Parse(teamIdMatch.Groups[1].Value) : (int?)null;

                if (teamId == 1258505) // ID команды Газпром-Югра
                {
                    // Это наш клуб
                    continue;
                }

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

            // Если не нашли матч с участием Газпром-Югра, пропускаем
            if (team1Id == null && team2Id == null) return;

            // Заполняем названия (если одна из команд — наш клуб)
            bool isHome = false;
            if (team1Id == 1258505)
            {
                isHome = true;
                // Меняем местами, чтобы team1 был соперник, team2 - наш клуб? 
                // Для единообразия оставим как есть, просто запомним isHome
            }

            // Счёт
            var scoreNode = matchNode.SelectSingleNode(".//div[contains(@class, 'timetable__score-main')]");
            var score = scoreNode?.InnerText.Trim() ?? "-:-";

            // Ссылка на страницу матча
            var matchLink = matchNode.SelectSingleNode(".//a[contains(@class, 'timetable__score')]");
            var matchUrl = matchLink != null ? "https://superliga.rfs.ru" + matchLink.GetAttributeValue("href", "") : "";

            // Время матча
            var timeNode = matchNode.SelectSingleNode(".//span[contains(@class, 'timetable__time')]");
            var time = timeNode?.InnerText.Trim() ?? "";

            // Место проведения (стадион)
            var placeNode = matchNode.SelectSingleNode(".//div[contains(@class, 'timetable__place')]");
            var stadium = placeNode?.SelectSingleNode(".//span[contains(@class, 'timetable__place-name')]")?.InnerText.Trim() ?? "";
            var stadiumFull = placeNode?.GetAttributeValue("title", "") ?? stadium;

            // Город (можно попробовать извлечь из адреса или стадиона)
            var city = ExtractCity(stadiumFull);

            // Номер тура
            var roundNode = matchNode.SelectSingleNode(".//span[contains(@class, 'timetable__round')]");
            var roundText = roundNode?.InnerText.Trim() ?? "";
            var round = ParseInt(Regex.Match(roundText, @"\d+").Value);

            // Дата и день недели (нужно получить из контекста страницы)
            // Для простоты пока оставим пустыми, потом можно доработать
            var date = DateTime.Now.ToString("yyyy-MM-dd");
            var weekday = "";

            // Получаем день недели из родительских элементов
            var parent = matchNode.ParentNode;
            while (parent != null)
            {
                var dateHeader = parent.SelectSingleNode(".//h3[contains(@class, 'calendar__date')]");
                if (dateHeader != null)
                {
                    var dateText = dateHeader.InnerText.Trim();
                    // Парсим "25 августа 2025, понедельник"
                    var dateMatch = Regex.Match(dateText, @"(\d+)\s+(\w+)\s+(\d+),\s+(\w+)");
                    if (dateMatch.Success)
                    {
                        var day = dateMatch.Groups[1].Value;
                        var month = dateMatch.Groups[2].Value;
                        var year = dateMatch.Groups[3].Value;
                        weekday = dateMatch.Groups[4].Value;
                        date = $"{year}-{ParseMonth(month)}-{day.PadLeft(2, '0')}";
                    }
                    break;
                }
                parent = parent.ParentNode;
            }

            // Создаём объект матча
            var match = new Fifa_serv.Models.Match
            {
                Round = round,
                Team1 = team1Name ?? "Газпром-Югра",
                Team2 = team2Name ?? "Газпром-Югра",
                Score = score,
                Date = date,
                Weekday = weekday,
                Time = time,
                City = city,
                Stadium = stadium,
                StadiumAddress = stadiumFull,
                IsHome = isHome,
                Status = score == "-:-" ? "upcoming" : "finished",
                MatchUrl = matchUrl,
                Team1Logo = !string.IsNullOrEmpty(team1Logo) ? await DownloadImageAsBase64Async(team1Logo) : "",
                Team2Logo = !string.IsNullOrEmpty(team2Logo) ? await DownloadImageAsBase64Async(team2Logo) : ""
            };

            match.Hash = _hashService.ComputeHash(match);

            // Сохраняем в базу
            var existing = _db.Matches.FindOne(x => x.MatchUrl == matchUrl);
            if (existing != null)
            {
                match.Id = existing.Id;
                _db.Matches.Update(match);
                Console.WriteLine($"  Обновлён матч: {match.Team1} vs {match.Team2}");
            }
            else
            {
                _db.Matches.Insert(match);
                Console.WriteLine($"  Добавлен матч: {match.Team1} vs {match.Team2}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при парсинге матча: {ex.Message}");
        }
    }

    private string ExtractCity(string stadiumInfo)
    {
        if (string.IsNullOrEmpty(stadiumInfo)) return "";

        // Пример: "ФСК для занятия мини-футболом «Сибиряк», Новосибирск, ул. Аэропорт 88/1"
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
}