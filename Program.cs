using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Demo.Data;
using Demo.Models;
using Demo.Middleware;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Security.Claims;
using System.Diagnostics;

class Program
{
    static async Task Main(string[] args)
    {
        // Create and configure the web application
        var builder = WebApplication.CreateBuilder(args);

        // Configure Kestrel for localhost only with default ports
        builder.WebHost.ConfigureKestrel(options =>
        {
            // Configure server limits for production
            options.Limits.MaxConcurrentConnections = 1000;
            options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
            options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5);
            options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(2);
        });

        // Add services to the container
        builder.Services.AddRazorPages(options =>
        {
            options.Conventions.AuthorizePage("/Admin/Users");
            options.Conventions.AllowAnonymousToPage("/Account/Login");
            options.Conventions.AllowAnonymousToPage("/Account/Register");
            options.Conventions.AllowAnonymousToPage("/Account/Logout");
            options.Conventions.AllowAnonymousToPage("/Error");
            options.Conventions.AllowAnonymousToPage("/");
        });

        // Configure authentication with cookie settings - FIXED
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.ExpireTimeSpan = TimeSpan.FromHours(24);
                options.SlidingExpiration = true;

                options.Cookie.Name = "UserManagementAuth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.None; // FIXED: Allow HTTP
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.IsEssential = true;
            });

        // Configure Entity Framework with connection string from appsettings
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 33)),
                mySqlConfig => mySqlConfig
                    .EnableRetryOnFailure(3, TimeSpan.FromSeconds(10), null) // FIXED: Reduced retry attempts
            ).EnableServiceProviderCaching();

            if (builder.Environment.IsDevelopment())
                options.EnableSensitiveDataLogging();
        });

        // Add CORS for external access
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        // Add memory cache and response compression
        builder.Services.AddMemoryCache();
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
        });

        // Configure logging
        builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.AddDebug();
            logging.SetMinimumLevel(LogLevel.Information);
        });

        // Build the application
        var app = builder.Build();

        // Database initialization and seeding - FIXED
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var initialLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                initialLogger.LogInformation("Checking database connection...");
                
                // Test database connection first
                await context.Database.CanConnectAsync();
                initialLogger.LogInformation("Database connection successful");

                // Apply migrations instead of just ensuring database exists
                initialLogger.LogInformation("Applying database migrations...");
                await context.Database.MigrateAsync();
                initialLogger.LogInformation("Database migrations applied successfully");

                // Check and create admin user
                var existingAdmin = await context.Users.FirstOrDefaultAsync(u => u.Email == "admin@gmail.com");
                if (existingAdmin == null)
                {
                    var adminUser = new User
                    {
                        Name = "Administrator",
                        Email = "admin@gmail.com",
                        PasswordHash = HashPassword("123"),
                        DateOfBirth = new DateTime(1990, 1, 1),
                        MobileNumber = "1234567890",
                        Gender = "Other",
                        Status = UserStatus.Active,
                        LastLogin = DateTime.UtcNow
                    };

                    context.Users.Add(adminUser);
                    await context.SaveChangesAsync();
                    initialLogger.LogInformation("Admin user created successfully");
                }
                else
                {
                    initialLogger.LogInformation("Admin user already exists with ID: {AdminId}", existingAdmin.Id);
                }

                // Verify admin user password hash
                var admin = await context.Users.FirstOrDefaultAsync(u => u.Email == "admin@gmail.com");
                if (admin != null)
                {
                    var testHash = HashPassword("123");
                    initialLogger.LogInformation("Admin password hash verification: {Match}", admin.PasswordHash == testHash);
                }
            }
            catch (Exception ex)
            {
                initialLogger.LogError(ex, "Critical error during database initialization");
                throw; // Stop application if database fails
            }
        }

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseCors();

        // FIXED: Don't force HTTPS redirect in development
        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.UseResponseCompression();

        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=86400");
            }
        });

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseUserStatusCheck();

        // FIXED: Proper root redirect
        app.MapGet("/", (HttpContext context) =>
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                return Results.Redirect("/Admin/Users");
            }
            return Results.Redirect("/Account/Login");
        });

        app.MapGet("/health", () => new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Server = Environment.MachineName,
            Version = "1.0.0",
            Environment = app.Environment.EnvironmentName
        });

        app.MapRazorPages();

        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("=== User Management System Started ===");
        logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
        logger.LogInformation("Available URLs:");
        logger.LogInformation("  - Local HTTP: http://localhost:5005");
        logger.LogInformation("  - Local HTTPS: https://localhost:5003");
        logger.LogInformation("  - Network HTTPS: https://192.168.31.8:7272");
        logger.LogInformation("Admin credentials: admin@gmail.com / 123");
        logger.LogInformation("Test endpoints:");
        logger.LogInformation("  - Health check: /health");
        logger.LogInformation("=== Application Ready ===");

        await app.RunAsync();
    }

    static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}