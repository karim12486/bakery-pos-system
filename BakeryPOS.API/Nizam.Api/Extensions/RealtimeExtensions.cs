using Nizam.Api.Hubs;

namespace Nizam.Api.Extensions;

public static class RealtimeExtensions
{
    /// <summary>Registers SignalR.</summary>
    public static IServiceCollection AddNizamRealtime(this IServiceCollection services)
    {
        services.AddSignalR();
        return services;
    }

    /// <summary>Maps the SignalR hub endpoints.</summary>
    public static WebApplication MapNizamHubs(this WebApplication app)
    {
        app.MapHub<RemovalHub>("/hubs/removal");
        app.MapHub<KdsHub>("/hubs/kds");
        return app;
    }
}
