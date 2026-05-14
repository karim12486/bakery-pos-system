using Microsoft.Extensions.FileProviders;

namespace Nizam.Api.Extensions;

public static class StaticFilesExtensions
{
    /// <summary>
    /// Configures static file serving from <c>{ContentRoot}/wwwroot</c> and ensures the
    /// <c>wwwroot/images</c> directory exists. Robust against the .exe-deployment scenario
    /// where the working directory may not be the content root.
    /// </summary>
    public static WebApplication UseNizamStaticFiles(this WebApplication app)
    {
        var contentRoot = app.Environment.ContentRootPath;
        var webRootPath = Path.Combine(contentRoot, "wwwroot");
        var imagesPath = Path.Combine(webRootPath, "images");

        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Static files content root: {ContentRoot}", contentRoot);
        logger.LogInformation("Static files webroot path: {WebRootPath}", webRootPath);

        if (!Directory.Exists(webRootPath))
        {
            logger.LogInformation("Creating missing static-files webroot directory at {Path}", webRootPath);
            Directory.CreateDirectory(webRootPath);
        }
        if (!Directory.Exists(imagesPath))
        {
            logger.LogInformation("Creating missing images directory at {Path}", imagesPath);
            Directory.CreateDirectory(imagesPath);
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(webRootPath),
            RequestPath = ""
        });

        return app;
    }
}
