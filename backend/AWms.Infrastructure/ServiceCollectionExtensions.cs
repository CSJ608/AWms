using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using AWms.Domain.Interfaces;
using AWms.Infrastructure.Data;
using AWms.Infrastructure.Services;

namespace AWms.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TokenService.JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<AdminSeedOptions>(configuration.GetSection("Admin"));

        var connStr = configuration.GetConnectionString("Default")
                      ?? throw new InvalidOperationException("Connection string 'Default' not found");

        services.AddDbContext<AWmsDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(connStr, npgsqlOpts =>
            {
                npgsqlOpts.MigrationsAssembly("AWms.Infrastructure");
            });
        });

        // 认证权限
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<TokenService>();
        services.AddSingleton<ITokenService>(sp => sp.GetRequiredService<TokenService>());
        services.AddScoped<AuthService>();
        services.AddScoped<NumberingService>();
        services.AddScoped<IQueryService, QueryService>();
        services.AddScoped<MasterDataService>();
        services.AddScoped<INumberService>(sp => sp.GetRequiredService<NumberingService>());
        services.AddScoped<ImportExportService>();
        services.AddScoped<IdempotencyService>();
        services.AddScoped<AdminSeedService>();
        services.AddScoped<InboundOrderService>();
        services.AddScoped<ReceiptService>();
        services.AddScoped<AttachmentService>();
        services.AddScoped<PrintService>();
        services.AddScoped<ScanService>();

        return services;
    }
}




