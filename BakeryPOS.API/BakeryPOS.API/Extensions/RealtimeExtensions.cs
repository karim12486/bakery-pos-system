using BakeryPOS.API.Hubs;

namespace BakeryPOS.API.Extensions;

public static class RealtimeExtensions
{
    /// <summary>Registers SignalR.</summary>
    public static IServiceCollection AddBakeryPosRealtime(this IServiceCollection services)
    {
        services.AddSignalR();
        return services;
    }

    /// <summary>Maps the SignalR hub endpoints.</summary>
    public static WebApplication MapBakeryPosHubs(this WebApplication app)
    {
        app.MapHub<RemovalHub>("/hubs/removal");
        return app;
    }
}
