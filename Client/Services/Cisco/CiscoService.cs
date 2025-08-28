using System.Net.Http.Json;
using eCommerce.Shared.Models;
using Microsoft.AspNetCore.Components.Forms;


namespace eCommerce.Client.Services.Cisco;

public class CiscoService 
{
    private readonly HttpClient _http;
    private const long MAX_ALLOWED_SIZE = 50 * 1024 * 1024; // 50MB

    public CiscoService(HttpClient http)
    {
        _http = http;
    }

    public async Task<JobFilaResponse> EnviarListaReal(IBrowserFile arquivoProdutos)
    {
        var formData = new MultipartFormDataContent();
        formData.Add(new StreamContent(arquivoProdutos.OpenReadStream(MAX_ALLOWED_SIZE)), "arquivoProdutos", arquivoProdutos.Name);
        var response = await _http.PostAsync("api/cisco/real", formData);
        
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

    //public async Task<JobFilaResponse> EnviarListaDolar(IBrowserFile arquivoProdutos, double valorDolar)
    public void EnviarListaDolar(IBrowserFile arquivoProdutos, double valorDolar)
    {
        var formData = new MultipartFormDataContent();
        formData.Add(new StreamContent(arquivoProdutos.OpenReadStream(MAX_ALLOWED_SIZE)), "arquivoProdutos", arquivoProdutos.Name);
        formData.Add(new StringContent(valorDolar.ToString()), "valorDolar");
        //var response = await _http.PostAsync("api/cisco/dolar", formData);
        
        //if (response.IsSuccessStatusCode)
        //{
        //    var job = await response.Content.ReadFromJsonAsync<JobFilaResponse>();
        //    return job ?? throw new HttpRequestException("Resposta vazia do servidor");
        //}
        //else
        //{
        //    throw new HttpRequestException($"Erro ao enviar produtos: {response.StatusCode}");
        //}
    }
}