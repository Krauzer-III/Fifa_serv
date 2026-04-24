using LiteDB;

namespace Fifa_serv.Models;

public class Player
{
    [BsonId]
    public int Id { get; set; }
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;      // Имя + Фамилия + Отчество
    public string Position { get; set; } = string.Empty;
    public int? Age { get; set; }                              // nullable, т.к. некоторые данные могут отсутствовать
    public string BirthDate { get; set; } = string.Empty;      // дата рождения
    public int Games { get; set; }                             // игры
    public int Goals { get; set; }                             // голы
    public int Assists { get; set; }                           // передачи
    public int YellowCards { get; set; }                       // жёлтые карточки
    public int RedCards { get; set; }                          // красные карточки
    public int? GoalsConceded { get; set; }                    // пропущенные голы (только для вратарей)
    public string PhotoBase64 { get; set; } = string.Empty;    // фото в base64
    public string PlayerUrl { get; set; } = string.Empty;      // ссылка на страницу игрока
    public string Hash { get; set; } = string.Empty;
}