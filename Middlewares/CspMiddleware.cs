using System.Security.Cryptography;

namespace PuzzleWebApp.Middlewares;

public class CspMiddleware
{
    private readonly RequestDelegate _next;

    public CspMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Generate a cryptographically secure random Nonce
        string nonce = GenerateNonce();

        // Store the nonce in HttpContext.Items so Views and Partials can read it
        context.Items["ScriptNonce"] = nonce;

        // Define your CSP rules. Inline styles are enabled for server-rendered puzzle layout values.
        string cspHeader = $"default-src 'self'; " +
                            $"script-src 'self' 'nonce-{nonce}'; " +
                    $"style-src 'self' 'unsafe-inline'; " +
                            $"img-src 'self' data: https:; " +
                            $"font-src 'self' data:; " +
#if DEBUG
                            $"connect-src 'self' ws: wss:; " +
#endif
                            $"object-src 'self' blob:; ";

#if DEBUG
        context.Response.Headers.Append("Content-Security-Policy-Report-Only", cspHeader);
#else
        context.Response.Headers.Append("Content-Security-Policy", cspHeader);
#endif

        await _next(context);
    }

    private string GenerateNonce()
    {
        byte[] byteArray = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(byteArray);
        }
        return Convert.ToBase64String(byteArray)
                      .Replace("=", "")
                      .Replace("+", "")
                      .Replace("/", ""); // Clean strings only
    }
}