// File: Middleware/UserStatusCheckMiddleware.cs
using System.Security.Claims;
using Demo.Data;
using Demo.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Demo.Middleware
{
    public class UserStatusCheckMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<UserStatusCheckMiddleware> _logger;

        public UserStatusCheckMiddleware(RequestDelegate next, IServiceScopeFactory serviceScopeFactory, ILogger<UserStatusCheckMiddleware> logger)
        {
            _next = next;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip check for specific paths and static files
            var path = context.Request.Path.Value?.ToLower();
            if (ShouldSkipUserCheck(path))
            {
                await _next(context);
                return;
            }

            // Check if user is authenticated
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    try
                    {
                        var user = await dbContext.Users.FindAsync(userId);

                        // Handle different user scenarios
                        if (user == null)
                        {
                            // User not found in database (might have been deleted)
                            _logger.LogWarning("User {UserId} not found in database, signing out", userId);
                            await SignOutUserAsync(context, "Your account no longer exists. Please register again.");
                            return;
                        }

                        switch (user.Status)
                        {
                            case UserStatus.Blocked:
                                _logger.LogWarning("Blocked user {UserId} ({Email}) attempted to access protected resource", userId, user.Email);
                                await SignOutUserAsync(context, "Your account has been blocked. Please contact administrator.");
                                return;

                            case UserStatus.Deleted:
                                // Remove deleted user from database and sign out
                                _logger.LogWarning("Deleted user {UserId} ({Email}) attempted access, removing from database", userId, user.Email);
                                dbContext.Users.Remove(user);
                                await dbContext.SaveChangesAsync();
                                await SignOutUserAsync(context, "Your account has been deleted. Please register again to create a new account.");
                                return;

                            case UserStatus.Active:
                                // User is active, continue processing
                                await _next(context);
                                return;

                            default:
                                _logger.LogWarning("User {UserId} ({Email}) has invalid status {Status}", userId, user.Email, user.Status);
                                await SignOutUserAsync(context, "Your account status is invalid. Please contact support.");
                                return;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error checking user status for user {UserId}", userId);
                        // Continue without blocking in case of database issues
                        // This ensures the application remains functional even if there are temporary DB issues
                        await _next(context);
                        return;
                    }
                }
            }

            await _next(context);
        }

        private static bool ShouldSkipUserCheck(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return true;

            // Paths that should skip user status checking
            var skipPaths = new[]
            {
                "/account/login",
                "/account/register", 
                "/account/logout",
                "/error",
                "/health",
                "/favicon.ico"
            };

            // Static file extensions that should be skipped
            var skipExtensions = new[]
            {
                ".css", ".js", ".ico", ".png", ".jpg", ".jpeg", ".gif", ".svg", 
                ".woff", ".woff2", ".ttf", ".eot", ".map"
            };

            // Paths that should be skipped
            var skipFolders = new[]
            {
                "/lib/", "/_framework/", "/css/", "/js/", "/images/"
            };

            return skipPaths.Any(p => path.Contains(p)) ||
                   skipExtensions.Any(ext => path.EndsWith(ext)) ||
                   skipFolders.Any(folder => path.Contains(folder));
        }

        private async Task SignOutUserAsync(HttpContext context, string message)
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            // Clear any existing session data
            context.Session?.Clear();
            
            // Redirect to login with message
            var loginUrl = $"/Account/Login?message={Uri.EscapeDataString(message)}";
            context.Response.Redirect(loginUrl);
        }
    }

    public static class UserStatusCheckMiddlewareExtensions
    {
        public static IApplicationBuilder UseUserStatusCheck(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<UserStatusCheckMiddleware>();
        }
    }
}