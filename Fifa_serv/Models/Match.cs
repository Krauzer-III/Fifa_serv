using LiteDB;

namespace Fifa_serv.Models;

public class Match
{
    [BsonId]
    public int Id { get; set; }
    public int Round { get; set; }
    public string MatchType { get; set; } = string.Empty; // Регулярный или плейоф
    public string Team1 { get; set; } = string.Empty;
    public string Team2 { get; set; } = string.Empty;
    public string Score { get; set; } = string.Empty;          // "3:2" или "-:-"
    public string Date { get; set; } = string.Empty;           
    public string Weekday { get; set; } = string.Empty;        
    public string Time { get; set; } = string.Empty;           // 13:00
    public string City { get; set; } = string.Empty;
    public string Stadium { get; set; } = string.Empty;        // название стадиона
    public string StadiumAddress { get; set; } = string.Empty; // адрес (если есть)
    public bool IsHome { get; set; }
    public string Status { get; set; } = "upcoming";           // finished / upcoming
    public string MatchUrl { get; set; } = string.Empty;       // ссылка на страницу матча
    public string Team1Logo { get; set; } = string.Empty;      // base64 или url
    public string Team2Logo { get; set; } = string.Empty;      // base64 или url
    public string Hash { get; set; } = string.Empty;
}