using System.Net.Http.Json;
using eCommerce.Shared.Models;
using Microsoft.AspNetCore.Components.Forms;


namespace eCommerce.Client.Services.HP;

public class HPService 
{
    
    private readonly HttpClient _http;
    private const long MAX_ALLOWED_SIZE = 50 * 1024 * 1024; // 50MB

    public HPService(HttpClient http)
    {
        _http = http;
    }

    public async Task<JobFilaResponse> EnviarProdutos(IBrowserFile arquivoProdutos, IBrowserFile arquivoPrecos)
    {
        var formData = new MultipartFormDataContent();
        formData.Add(new StreamContent(arquivoProdutos.OpenReadStream(MAX_ALLOWED_SIZE)), "arquivoProdutos", arquivoProdutos.Name);
        formData.Add(new StreamContent(arquivoPrecos.OpenReadStream(MAX_ALLOWED_SIZE)), "arquivoPrecos", arquivoPrecos.Name);
        var response = await _http.PostAsync("api/hp/produtos", formData);
        
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
        var formData = new MultipartFormDataContent();
        formData.Add(new StreamContent(arquivoProdutos.OpenReadStream(MAX_ALLOWED_SIZE)), "arquivoProdutos", arquivoProdutos.Name);
        var response = await _http.PostAsync("api/hp/carepack", formData);
        
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

    public async Task<JobFilaResponse> EnviarPlotter(IBrowserFile arquivoProdutos)
    {
        var formData = new MultipartFormDataContent();
        formData.Add(new StreamContent(arquivoProdutos.OpenReadStream(MAX_ALLOWED_SIZE)), "arquivoProdutos", arquivoProdutos.Name);
        var response = await _http.PostAsync("api/hp/plotter", formData);
        
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

    public async Task<JobFilaResponse> EnviarPromocao(IBrowserFile arquivoProdutos)
    {
        var formData = new MultipartFormDataContent();
        formData.Add(new StreamContent(arquivoProdutos.OpenReadStream(MAX_ALLOWED_SIZE)), "arquivoProdutos", arquivoProdutos.Name);
        var response = await _http.PostAsync("api/hp/promocao", formData);

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