using Fifa_serv.Background;
using Fifa_serv.Services;

namespace Fifa_serv.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFifaDataSync(this IServiceCollection services)
    {
        services.AddScoped<EntityHashDiffService>();
        services.AddScoped<CommonHashService>();
        services.AddHostedService<MoscowMidnightRefreshService>();
        return services;
    }

    private static void ConfigureHttpClient(HttpClient client)
    {
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 Fifa_serv/1.0");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9");
    }
}
