using Duende.IdentityServer.AspNetIdentity;
using Duende.IdentityServer.Services;
using Identity.API;
using Identity.API.Configuration;
using Identity.API.Database;
using Identity.API.Models;
using Identity.API.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Identity.API.Extension;

namespace Identity.API
{
    public class Program
    {
        public static int Main(string[] args)
        {
            string appName = typeof(Program).Namespace;
            var configuration = GetConfiguration();

            // Set up Serilog
            Log.Logger = CreateSerilogLogger(configuration);

            try
            {
                Log.Information("Configuring web host ({ApplicationContext})...", appName);

                // Create web application builder
                var builder = WebApplication.CreateBuilder(args);

                string migrationAssembly = Assembly.GetAssembly(typeof(Program)).GetName().Name;
                string connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

                // Add services to the container
                builder.Services.AddOpenApi();
                builder.Services.AddControllersWithViews();
                builder.Services.AddRazorPages();

                // Configure ApplicationDbContext with retry policy and migrations
                builder.Services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(
                        connectionString,
                        sqlServerOptionsAction: sqlOptions =>
                        {
                            sqlOptions.MigrationsAssembly(migrationAssembly);
                            sqlOptions.EnableRetryOnFailure(
                                maxRetryCount: 5,
                                maxRetryDelay: TimeSpan.FromSeconds(30),
                                errorNumbersToAdd: null
                            );
                        }));

                // Configure Identity with ApplicationUser and IdentityRole
                builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
                    .AddEntityFrameworkStores<ApplicationDbContext>()
                    .AddDefaultTokenProviders();

                // Configure IdentityServer with AspNetIdentity
                builder.Services.AddIdentityServer(x =>
                    {
                        x.IssuerUri = "http://localhost:5000";
                        x.Authentication.CookieLifetime = TimeSpan.FromHours(30);
                    })
                    .AddDeveloperSigningCredential()
                    .AddAspNetIdentity<ApplicationUser>()
                    .AddConfigurationStore(options =>
                    {
                        options.ConfigureDbContext = builder => builder.UseSqlServer(connectionString,
                            sqlServerOptionsAction: sqlOptions =>
                            {
                                sqlOptions.MigrationsAssembly(migrationAssembly);
                                sqlOptions.EnableRetryOnFailure(
                                    maxRetryCount: 5,
                                    maxRetryDelay: TimeSpan.FromSeconds(30),
                                    errorNumbersToAdd: null
                                );
                            });
                    })
                    .AddOperationalStore(options =>
                    {
                        options.ConfigureDbContext = builder => builder.UseSqlServer(connectionString,
                            sqlServerOptionsAction: sqlOptions =>
                            {
                                sqlOptions.MigrationsAssembly(migrationAssembly);
                                sqlOptions.EnableRetryOnFailure(
                                    maxRetryCount: 5,
                                    maxRetryDelay: TimeSpan.FromSeconds(30),
                                    errorNumbersToAdd: null
                                );
                            });
                    })
                    .Services.AddTransient<IProfileService, ProfileService>();

                // Build the app
                var app = builder.Build();

                // Apply migrations
                Log.Information("Applying migrations ({ApplicationContext})...", appName);
                app.MigrateDbContext<PersistedGrantDbContext>((_, __) => { })
                    .MigrateDbContext<ApplicationDbContext>((context, services) =>
                    {
                        var env = services.GetService<IWebHostEnvironment>();
                        var logger = services.GetService<ILogger<ApplicationDbContextSeed>>();
                        var settings = services.GetService<IOptions<AppSettings>>();

                        new ApplicationDbContextSeed()
                            .SeedAsync(context, env, logger, settings)
                            .Wait();
                    })
                    .MigrateDbContext<ConfigurationDbContext>((context, services) =>
                    {
                        new ConfigurationDbContextSeed()
                            .SeedAsync(context, configuration)
                            .Wait();
                    });

                // Configure the HTTP request pipeline
                if (app.Environment.IsDevelopment())
                {
                    app.UseDeveloperExceptionPage();
                    app.UseSwagger();
                    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Identity.API v1"));
                    app.MapOpenApi();
                }

                app.UseHttpsRedirection();
                app.UseAuthentication();
                app.UseRouting();
                app.UseIdentityServer();
                app.UseAuthorization();
                app.MapControllers();

                // Run the application
                Log.Information("Starting web host ({ApplicationContext})...", appName);
                app.Run();

                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Program terminated unexpectedly ({ApplicationContext})!", appName);
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        // Create and configure Serilog logger
        private static Serilog.ILogger CreateSerilogLogger(IConfiguration configuration)
        {
            return new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.WithProperty("ApplicationContext", typeof(Program).Namespace)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
        }

        // Get configuration from appsettings and environment variables
        private static IConfiguration GetConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddUserSecrets(Assembly.GetAssembly(typeof(Program)));

            var config = builder.Build();
            return config;
        }
    }
}