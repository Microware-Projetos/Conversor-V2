using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using LiteDB;
using eCommerce.Server.Services.Job;
using eCommerce.Server.Services.Bling;
using eCommerce.Server.Services.HP;
using eCommerce.Server.Services.Lenovo;

namespace eCommerce.Server.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCustomServices(this IServiceCollection services)
        {
            // Limites de upload
            services.Configure<IISServerOptions>(options =>
            {
                options.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB
            });
            services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB
            });
            services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50MB
                options.ValueLengthLimit = int.MaxValue;
                options.MemoryBufferThreshold = int.MaxValue;
            });

            // Serviços customizados
            services.AddHostedService<JobWorker>();
            services.AddScoped<JobWorker>();
            services.AddScoped<BlingService>();
            services.AddScoped<HPService>();
            services.AddScoped<LenovoService>();

            // HttpClient
            services.AddHttpClient();

            // CORS
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            // LiteDB
            services.AddSingleton<LiteDatabase>(provider =>
                new LiteDatabase("Filename=fila.db;Connection=shared"));

            return services;
        }
    }
} 