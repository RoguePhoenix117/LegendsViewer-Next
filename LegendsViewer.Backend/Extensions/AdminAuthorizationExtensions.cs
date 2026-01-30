using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;

namespace LegendsViewer.Backend.Extensions;

public static class AdminAuthorizationExtensions
{
    public static bool IsAdmin(this HttpContext context, IConfiguration configuration)
    {
        var adminKey = configuration["AdminApiKey"];
        
        // If no admin key is configured, allow all (development mode)
        if (string.IsNullOrWhiteSpace(adminKey))
        {
            return true;
        }

        // Check for admin key in query parameter or header
        var providedKey = context.Request.Query["adminKey"].FirstOrDefault() 
                       ?? context.Request.Headers["X-Admin-Key"].FirstOrDefault();

        return providedKey == adminKey;
    }
}
