
using LegendsViewer.Backend.Extensions;
using LegendsViewer.Backend.Legends;
using LegendsViewer.Backend.Legends.Bookmarks;
using LegendsViewer.Backend.Legends.Interfaces;
using LegendsViewer.Backend.Legends.Maps;
using LegendsViewer.Backend.Logging;
using LegendsViewer.Backend.Legends.Translations;
using LegendsViewer.Frontend;
using Microsoft.Extensions.Logging.Console;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.FileProviders;

namespace LegendsViewer.Backend;

public class Program
{
    private const string AllowAllOriginsPolicy = "AllowAllOrigins";

    public static void Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var builder = WebApplication.CreateBuilder(args);
        
        var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();

        builder.Services.AddCors(o => o.AddPolicy(AllowAllOriginsPolicy, policy =>
        {
            if (corsOrigins.Length == 0 || corsOrigins.Contains("*"))
            {
                // Development: allow all origins
                policy.AllowAnyOrigin();
            }
            else
            {
                // Production: specific origins
                policy.WithOrigins(corsOrigins);
            }
            policy.AllowAnyMethod()
                  .AllowAnyHeader();
        }));

        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5);
            serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
            // Allow large file uploads (up to 1 GB for world export files)
            serverOptions.Limits.MaxRequestBodySize = 1024L * 1024L * 1024L; // 1 GB
        });

builder.Services.AddSingleton<IWorld>(sp =>
{
    var dictionary = sp.GetRequiredService<IDwarvenDictionary>();
    return new World(dictionary);
});
        builder.Services.AddSingleton<IWorldMapImageGenerator, WorldMapImageGenerator>();
        builder.Services.AddSingleton<IDwarvenDictionary, DwarvenDictionary>();
        builder.Services.AddSingleton<IBookmarkService, BookmarkService>();
        builder.Services.AddClassicRepositories();

        // Add response compression for JSON and other text responses
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
            options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
        });

        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                // Serialize Enums as strings globally
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // Add response caching
        builder.Services.AddResponseCaching();

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options => options.FormatterName = nameof(SimpleLogFormatter));
        builder.Logging.AddConsoleFormatter<SimpleLogFormatter, ConsoleFormatterOptions>();

        var app = builder.Build();

        // Ensure data directory exists
        var dataDirectory = app.Configuration["DataDirectory"] ?? "/app/data";
        Directory.CreateDirectory(dataDirectory);
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation($"Data directory configured: {dataDirectory}");

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Enable response compression (must be before UseRouting/MapControllers)
        app.UseResponseCompression();

        // Enable response caching
        app.UseResponseCaching();

        app.UseCors(AllowAllOriginsPolicy);

        app.UseAuthorization();
        app.MapControllers();

        logger.LogInformation(AsciiArt.LegendsViewerLogo);

        // Serve frontend static files
        var frontendPath = Path.Combine(
            app.Environment.ContentRootPath,
            "legends-viewer-frontend",
            "dist"
        );

        if (Directory.Exists(frontendPath))
        {
            app.UseDefaultFiles(new DefaultFilesOptions 
            { 
                DefaultFileNames = ["index.html"] 
            });
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(frontendPath),
                RequestPath = ""
            });
            
            // SPA fallback - serve index.html for all non-API routes
            app.MapFallbackToFile("index.html", new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(frontendPath)
            });
        }
        app.Run();
    }
}
