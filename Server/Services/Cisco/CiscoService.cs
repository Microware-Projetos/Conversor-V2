using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;
using eCommerce.Shared.Models;
using LiteDB;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace eCommerce.Server.Services.Cisco;

public class CiscoService
{
    private const string FILE_PATH = "Cache/Cisco/dolar.json";
    private const string WOOCOMMERCE_CONSUMER_KEY = "ck_6a34c9bc2b5ea7c14d065b8614b813608b143643";
    private const string WOOCOMMERCE_CONSUMER_SECRET = "cs_744adb92980cf60007be9e3994ba1e13ae2ad1be";
    private const string WOOCOMMERCE_BASE_URL = "https://ecommerce.microware.com.br/cisco/wp-json/wc/v3";

    private readonly LiteDatabase _db;
    private readonly HttpClient _http;
    
    public CiscoService(LiteDatabase db, HttpClient http)
    {
        _db = db;
        _http = http;
        
        // Configurar autenticação Basic para todas as requisições
        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{WOOCOMMERCE_CONSUMER_KEY}:{WOOCOMMERCE_CONSUMER_SECRET}")
        );
        _http.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Basic", credentials);
    }

    public static double GetDolarValue()
    {
        try
        {
            if (!File.Exists(FILE_PATH))
                return 5.0;

            var json = File.ReadAllText(FILE_PATH);
            var data = System.Text.Json.JsonSerializer.Deserialize<DolarData>(json);

            if (data != null && data.valor > 0)
                return data.valor;

            return 5.0;
        }
        catch
        {
            return 5.0;
        }
    }

    public void SaveDolarValue(double valor)
    {
        try
        {
            var data = new { valor = valor };
            var json = System.Text.Json.JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            
            // Criar diretório se não existir
            var directory = Path.GetDirectoryName(FILE_PATH);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
                
            File.WriteAllText(FILE_PATH, json);
        }
        catch (Exception ex)
        {
            // Log do erro se necessário
            Console.WriteLine($"[ERROR]: Erro ao salvar valor do dólar: {ex.Message}");
        }
    }

    public async Task DeletarTodosProdutosCisco()
    {
        var urlBase = $"{WOOCOMMERCE_BASE_URL}/products";
        var todosIds = new List<int>();

        try
        {
            // Primeiro, coletamos todos os IDs
            var page = 1;
            while (true)
            {
                try
                {
                    var response = await _http.GetAsync($"{urlBase}?per_page=100&page={page}");
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        throw new Exception("[ERROR]: Erro de Autenticação. Verifique as credenciais da API.");
                    else if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        throw new Exception($"[ERROR]: Erro na API: {(int)response.StatusCode} - {errorContent}");
                    }
                    
                    var content = await response.Content.ReadAsStringAsync();
                    var produtos = System.Text.Json.JsonSerializer.Deserialize<List<WooCommerceProduct>>(content);
                    
                    if (produtos == null || !produtos.Any())
                        break;
                    
                    // Coleta todos os produtos sem filtrar por categoria
                    foreach (var produto in produtos)
                    {
                        todosIds.Add(produto.id);
                    }
                    
                    page++;
                }
                catch (HttpRequestException ex)
                {
                    throw new Exception($"[ERROR]: Erro de conexão com a API: {ex.Message}");
                }
            }

            if (!todosIds.Any())
            {
                Console.WriteLine("[INFO]: Nenhum produto encontrado.");
                return;
            }

            Console.WriteLine($"[INFO]: Encontrados {todosIds.Count} produtos para deletar.");

            // Agora deletamos em grupos de 5
            for (int i = 0; i < todosIds.Count; i += 5)
            {
                var grupoIds = todosIds.Skip(i).Take(5).ToList();
                var tarefas = grupoIds.Select(id => DeletarProduto(id)).ToList();
                
                await Task.WhenAll(tarefas);
                Console.WriteLine($"[INFO]: Grupo de {grupoIds.Count} produtos processado.");
            }

            Console.WriteLine("[INFO]: Todos os produtos foram deletados com sucesso!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR]: Erro ao deletar produtos: {ex.Message}");
            throw;
        }
    }

    private async Task DeletarProduto(int id)
    {
        Console.WriteLine("[INFO]: Deletando produtos...");
        try
        {
            var response = await _http.DeleteAsync($"{WOOCOMMERCE_BASE_URL}/products/{id}");
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[INFO]: Produto {id} deletado com sucesso.");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[ERROR]: Erro ao deletar produto {id}: {(int)response.StatusCode} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR]: Erro ao deletar produto {id}: {ex.Message}");
        }
    }

    public async Task<JobFilaResponse> EnviarListaReal(IFormFile arquivoProdutos)
    {
        Console.WriteLine("[INFO]: Enviando lista real...");
        var formData = new MultipartFormDataContent();
        formData.Add(new StreamContent(arquivoProdutos.OpenReadStream()), "arquivoProdutos", arquivoProdutos.FileName);
        
        var response = await _http.PostAsync($"{WOOCOMMERCE_BASE_URL}/products", formData);
        
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("[INFO]: Resposta da API: {response.StatusCode}");
            var content = await response.Content.ReadAsStringAsync();
            var job = System.Text.Json.JsonSerializer.Deserialize<JobFilaResponse>(content);
            return job ?? throw new HttpRequestException("[ERROR]: Resposta vazia do servidor");
        }
        else
        {
            Console.WriteLine("[ERROR]: Erro ao enviar produtos: {response.StatusCode}");
            throw new HttpRequestException($"[ERROR]: Erro ao enviar produtos: {response.StatusCode}");
        }
    }

    public async Task<JobFilaResponse> EnviarListaDolar(IFormFile arquivoProdutos, double valorDolar)
    {
        Console.WriteLine("[INFO]: Enviando lista dolar...");
        var formData = new MultipartFormDataContent();
        formData.Add(new StreamContent(arquivoProdutos.OpenReadStream()), "arquivoProdutos", arquivoProdutos.FileName);
        formData.Add(new StringContent(valorDolar.ToString()), "valorDolar");

        Console.WriteLine("[INFO]: Fazendo requisição para a API... {WOOCOMMERCE_BASE_URL}/products");
        var response = await _http.PostAsync($"{WOOCOMMERCE_BASE_URL}/products", formData);
        
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("[INFO]: Resposta da API: {response.StatusCode}");
            var content = await response.Content.ReadAsStringAsync();
            var job = System.Text.Json.JsonSerializer.Deserialize<JobFilaResponse>(content);
            return job ?? throw new HttpRequestException("[ERROR]: Resposta vazia do servidor");
        }
        else
        {
            Console.WriteLine("[ERROR]: Erro ao enviar produtos: {response.StatusCode}");
            throw new HttpRequestException($"[ERROR]: Erro ao enviar produtos: {response.StatusCode}");
        }
    }

    public async Task<JobFilaResponse> EnviarListaRealRevisao(IFormFile arquivoProdutos)
    {
        Console.WriteLine("[INFO]: Enviando lista real revisão...");
        var formData = new MultipartFormDataContent();
        formData.Add(new StreamContent(arquivoProdutos.OpenReadStream()), "arquivoProdutos", arquivoProdutos.FileName);
        
        Console.WriteLine("[INFO]: Fazendo requisição para a API... {WOOCOMMERCE_BASE_URL}/products");
        var response = await _http.PostAsync($"{WOOCOMMERCE_BASE_URL}/products", formData);
        
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("[INFO]: Resposta da API: {response.StatusCode}");
            var content = await response.Content.ReadAsStringAsync();
            var job = System.Text.Json.JsonSerializer.Deserialize<JobFilaResponse>(content);
            return job ?? throw new HttpRequestException("[ERROR]: Resposta vazia do servidor");
        }
        else
        {
            Console.WriteLine("[ERROR]: Erro ao enviar produtos: {response.StatusCode}");
            throw new HttpRequestException($"[ERROR]: Erro ao enviar produtos: {response.StatusCode}");
        }
    }

    private class DolarData
    {
        public double valor { get; set; }
    }
    
    private class WooCommerceProduct
    {
        public int id { get; set; }
        public string name { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
    }
}