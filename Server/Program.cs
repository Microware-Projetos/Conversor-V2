using Microsoft.AspNetCore.ResponseCompression;
using eCommerce.Server.Services.Job;
using eCommerce.Server.Services.HP;
using LiteDB;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Http.Features;
using eCommerce.Server.Services.Bling;
using eCommerce.Server.Services.HP;

var builder = WebApplication.CreateBuilder(args);

// Configurar limites de upload
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50MB
    options.ValueLengthLimit = int.MaxValue;
    options.MemoryBufferThreshold = int.MaxValue;
});

// Add services to the container.
builder.Services.AddHostedService<JobWorker>();
builder.Services.AddScoped<JobWorker>();
builder.Services.AddScoped<BlingService>();
builder.Services.AddScoped<HPService>();

// Adicionar HttpClient
builder.Services.AddHttpClient();

// Adicionar CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Registrar LiteDatabase
builder.Services.AddSingleton<LiteDatabase>(provider => 
    new LiteDatabase("Filename=fila.db;Connection=shared"));

// Registrar HPService
builder.Services.AddScoped<HPService>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// Usar CORS
app.UseCors("AllowAll");

app.UseRouting();

app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
