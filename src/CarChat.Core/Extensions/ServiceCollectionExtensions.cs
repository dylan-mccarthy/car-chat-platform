using CarChat.Core.Data;
using CarChat.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarChat.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCarChatCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "sqlite";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=carchat.db";

        services.AddDbContext<AppDbContext>(opts =>
        {
            if (provider.Equals("postgresql", StringComparison.OrdinalIgnoreCase))
                opts.UseNpgsql(connectionString);
            else
                opts.UseSqlite(connectionString);
        });

        services.AddScoped<IApiKeyService, ApiKeyService>();
        services.AddSingleton<ISessionManager, SessionManager>();

        return services;
    }
}
