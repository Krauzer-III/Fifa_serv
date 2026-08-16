using LiteDB;

namespace Fifa_serv.Models;

public class News
{
    [BsonId]
    public int Id { get; set; }

    /// <summary>
    /// Заголовок новости.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Категория: Команда, Тренеры, Дубль и т. д.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Ссылка на оригинальную публикацию.
    /// </summary>
    public string NewsUrl { get; set; } = string.Empty;

    /// <summary>
    /// Изображение в формате data:image/...;base64,...
    /// </summary>
    public string ImageBase64 { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public string Hash { get; set; } = string.Empty;
}