using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.Infrastructure.Services;

namespace SupportDesk.Infrastructure;

/// <summary>
/// Wires the domain's repositories and the application's abstractions to their SQL Server /
/// system implementations.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is missing. See README.md for local setup.");

        services.AddDbContext<SupportDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<SupportDbContext>());

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Security.Pbkdf2PasswordHasher>();

        var jwtSection = configuration.GetSection(Security.JwtOptions.SectionName);
        var defaultJwt = new Security.JwtOptions();
        var jwtOptions = new Security.JwtOptions
        {
            SecretKey = jwtSection["SecretKey"] ?? defaultJwt.SecretKey,
            Issuer = jwtSection["Issuer"] ?? defaultJwt.Issuer,
            Audience = jwtSection["Audience"] ?? defaultJwt.Audience,
            ExpiryHours = int.TryParse(jwtSection["ExpiryHours"], out var h) ? h : defaultJwt.ExpiryHours
        };
        services.AddSingleton(jwtOptions);
        services.AddSingleton<IJwtTokenGenerator, Security.JwtTokenGenerator>();

        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();

        services.AddScoped<ITicketQueries, TicketQueries>();
        services.AddScoped<ICustomerQueries, CustomerQueries>();
        services.AddScoped<IAgentQueries, AgentQueries>();
        services.AddScoped<ICategoryQueries, CategoryQueries>();

        services.AddScoped<SupportDbSeeder>();

        return services;
    }
}
