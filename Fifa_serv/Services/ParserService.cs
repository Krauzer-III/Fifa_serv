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
}