using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fifa_serv.Services;

public static class EntityHashFactory
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string Create<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, Options);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
