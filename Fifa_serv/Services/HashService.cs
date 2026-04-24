using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fifa_serv.Services;

public class HashService
{
    public string ComputeHash<T>(T data)
    {
        var json = JsonSerializer.Serialize(data);
        using var md5 = MD5.Create();
        var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLower();
    }

    public string ComputeHashFromList<T>(IEnumerable<T> data)
    {
        var json = JsonSerializer.Serialize(data);
        using var md5 = MD5.Create();
        var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLower();
    }
}