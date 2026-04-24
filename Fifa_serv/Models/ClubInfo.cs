using LiteDB;

namespace Fifa_serv.Models;

public class ClubInfo
{
    [BsonId]
    public int Id { get; set; }
    public string HistoryText { get; set; } = string.Empty;
    public string HeaderImageUrl { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public string TeamPhotoUrl { get; set; } = string.Empty;
    public string HeadCoach { get; set; } = string.Empty;
    public string President { get; set; } = string.Empty;
    public string CurrentPosition { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
}