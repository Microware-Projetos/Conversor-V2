using System.Net.Http.Json;
using eCommerce.Shared.Models;
using Microsoft.AspNetCore.Components.Forms;


namespace eCommerce.Client.Services.Job;

public class LimpezaJobsResponse
{
    public string message { get; set; } = "";
    public int jobsRemovidos { get; set; }
    public int jobsCancelados { get; set; }
}

public class JobService 
{
    
    private readonly HttpClient _http;

    public JobService(HttpClient http)
    {
        _http = http;
    }

    public async Task<JobFilaResponse> ConsultarJob(string id)
    {
        var response = await _http.GetFromJsonAsync<JobFilaResponse>($"api/job/{id}");
        return response ?? throw new HttpRequestException("Resposta vazia do servidor");
    }

    public async Task<List<JobFilaResponse>> ListarJobs()
    {
        var response = await _http.GetFromJsonAsync<List<JobFilaResponse>>("api/job");
        return response ?? throw new HttpRequestException("Resposta vazia do servidor");
    }

    public async Task<LimpezaJobsResponse> LimparTodosJobs()
    {
        var response = await _http.DeleteAsync("api/job/limpar-todos");
        
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<LimpezaJobsResponse>();
            return result ?? throw new HttpRequestException("Resposta vazia do servidor");
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Erro ao limpar jobs: {response.StatusCode} - {errorContent}");
        }
    }
}