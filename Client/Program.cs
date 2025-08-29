using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using eCommerce.Client;
using eCommerce.Client.Services.HP;
using eCommerce.Client.Services.Job;
using eCommerce.Client.Services.Base;
using eCommerce.Client.Services.Lenovo;
using eCommerce.Client.Services.Cisco;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<HPService>();
builder.Services.AddScoped<JobService>();
builder.Services.AddScoped<BaseService>();
builder.Services.AddScoped<LenovoService>();
builder.Services.AddScoped<CiscoService>();

await builder.Build().RunAsync();
