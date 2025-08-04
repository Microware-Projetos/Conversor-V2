using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using eCommerce.Server.Helpers;
using eCommerce.Server.Processors.Lenovo;

namespace eCommerce.Server.Processors.Lenovo;

public static class NormalizeUtisLenovo
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private static readonly string CACHE_FILE = "normalized_values_cache.json";
    private static readonly Dictionary<string, Dictionary<string, string>> _normalizedValuesCache = new();

    //TODO: Implementar BuscarImagens
    public static async Task<List<object>> BuscarImagens()
    {
        return new List<object>();
    }

    //TODO: Implementar BuscarDelivery
    public static async Task<List<object>> BuscarDelivery()
    {
        try
        {
            var url = "https://eprodutos-integracao.microware.com.br/api/delivery-info/";
            var response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var jsonArray = JArray.Parse(content);
                return jsonArray.ToObject<List<object>>();
            }
            
            Console.WriteLine($"Erro ao buscar delivery: Status {response.StatusCode}");
            return new List<object>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao buscar delivery: {ex.Message}");
            return new List<object>();
        }
        
    }

    public static Dictionary<string, string> NormalizeValuesList(string value)
    {
        Console.WriteLine($"\nIniciando normalização de valores para: {value}");

        // Verifica se o valor já está no cache
        if (_normalizedValuesCache.ContainsKey(value))
        {
            Console.WriteLine($"Valores de {value} encontrados no cache");
            return _normalizedValuesCache[value];
        }

        Dictionary<string, string> normalizeValuesList = new();

        try
        {
            var request = _httpClient.GetAsync("https://eprodutos-integracao.microware.com.br/api/normalize-values").Result;
            Console.WriteLine($"Status da resposta da API: {(int)request.StatusCode}");
            
            if (request.IsSuccessStatusCode)
            {
                Console.WriteLine("Resposta recebida da API, tentando processar...");
                try
                {
                    // Tenta processar a resposta em partes menores
                    var responseText = request.Content.ReadAsStringAsync().Result;
                    if (string.IsNullOrWhiteSpace(responseText))
                    {
                        Console.WriteLine("Resposta da API está vazia");
                        normalizeValuesList = new Dictionary<string, string>();
                    }
                    else
                    {
                        Console.WriteLine($"Tamanho da resposta: {responseText.Length} caracteres");
                        var responseData = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);

                        foreach (var item in responseData!)
                        {
                            if (item.Value is Dictionary<string, object> itemDict && 
                                itemDict.TryGetValue("column", out var columnObj) && 
                                columnObj?.ToString() == value)
                            {
                                if (itemDict.TryGetValue("fromTo", out var fromToObj) && 
                                    fromToObj is Dictionary<string, object> fromToList)
                                {
                                    foreach (var pair in fromToList)
                                    {
                                        normalizeValuesList[pair.Key] = pair.Value?.ToString() ?? "";
                                    }

                                    //Salva no cache persistente
                                    CacheManagerLenovo.SaveToCache(value, normalizeValuesList, CACHE_FILE);
                                    return normalizeValuesList;
                                }
                            }
                        }
                        Console.WriteLine("Nenhum item correspondente encontrado na resposta da API.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao processar resposta da API: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao processar resposta da API: {ex.Message}");
        }

        return new Dictionary<string, string>();
    }
}