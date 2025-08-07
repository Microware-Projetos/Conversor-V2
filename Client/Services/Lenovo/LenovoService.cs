using System.Net.Http.Json;
using eCommerce.Shared.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace eCommerce.Client.Services.Lenovo;

public class LenovoService
{
    private readonly HttpClient _http;
    private const long MAX_ALLOWED_SIZE = 50 * 1024 * 1024; // 50MB

    public LenovoService(HttpClient http)
    {
        _http = http;
    }

    public async Task<JobFilaResponse> EnviarProdutos(IBrowserFile arquivoProdutos)
    {
        Console.WriteLine("[INFO]: Processando arquivo Produtos");
        var formData = new MultipartFormDataContent();
        formData.Add(new StreamContent(arquivoProdutos.OpenReadStream(MAX_ALLOWED_SIZE)), "arquivoProdutos", arquivoProdutos.Name);
        var response = await _http.PostAsync("api/lenovo/produtos", formData);

        if (response.IsSuccessStatusCode)
        {
            var job = await response.Content.ReadFromJsonAsync<JobFilaResponse>();
            return job ?? throw new HttpRequestException("Resposta vazia do servidor");
        }
        else
        {
            throw new HttpRequestException($"Erro ao enviar produtos: {response.StatusCode}");
        }
    }

    public async Task<JobFilaResponse> EnviarCarePack(IBrowserFile arquivoProdutos)
    {
        Console.WriteLine("[INFO]: Processando arquivo CarePack");
        var formData = new MultipartFormDataContent();
        formData.Add(new StreamContent(arquivoProdutos.OpenReadStream(MAX_ALLOWED_SIZE)), "arquivoProdutos", arquivoProdutos.Name);
        var response = await _http.PostAsync("api/lenovo/carepack", formData);

        if (response.IsSuccessStatusCode)
        {
            var job = await response.Content.ReadFromJsonAsync<JobFilaResponse>();
            return job ?? throw new HttpRequestException("Resposta vazia do servidor");
        }
        else
        {
            throw new HttpRequestException($"Erro ao enviar produtos: {response.StatusCode}");
        }
    }
}