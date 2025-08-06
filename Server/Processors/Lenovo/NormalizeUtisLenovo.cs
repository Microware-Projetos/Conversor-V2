using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ClosedXML.Excel;
using eCommerce.Server.Helpers;
using eCommerce.Shared.Models;
using eCommerce.Server.Processors.Lenovo;

namespace eCommerce.Server.Processors.Lenovo;

public static class NormalizeUtisLenovo
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private static readonly string CACHE_FILE = "normalized_values_cache.json";
    private static readonly Dictionary<string, Dictionary<string, string>> _normalizedValuesCache = new();

    //TODO: Implementar BuscarImagens
    public static async Task<List<Dictionary<string, object>>> BuscarImagens()
    {
        Console.WriteLine("[INFO]: Buscando Imagens pelo eProdutos.");
        string url = "https://eprodutos-integracao.microware.com.br/api/photos/allId";

        try
        {
            Console.WriteLine("[INFO]: Pedindo response da url");
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[ERRO]: Requisição falhou com status {response.StatusCode}");
                return new List<Dictionary<string, object>>();
            }

            string json = await response.Content.ReadAsStringAsync();

            var lista = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);

            return lista ?? new List<Dictionary<string, object>>();

        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO]: Exceção ao buscar imagens: {ex.Message}");
            return new List<Dictionary<string, object>>();
        } 
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
        Console.WriteLine($"\n[INFO]: Iniciando normalização de valores para: {value}");

        // Verifica se o valor já está no cache
        if (_normalizedValuesCache.ContainsKey(value))
        {
            Console.WriteLine($"[INFO]: Valores de {value} encontrados no cache");
            return _normalizedValuesCache[value];
        }

        Dictionary<string, string> normalizeValuesList = new();

        try
        {
            var request = _httpClient.GetAsync("https://eprodutos-integracao.microware.com.br/api/normalize-values").Result;
            Console.WriteLine($"[INFO]: Status da resposta da API: {(int)request.StatusCode}");
            
            if (request.IsSuccessStatusCode)
            {
                Console.WriteLine("[INFO]: Resposta recebida da API, tentando processar...");
                try
                {
                    // Tenta processar a resposta em partes menores
                    var responseText = request.Content.ReadAsStringAsync().Result;
                    if (string.IsNullOrWhiteSpace(responseText))
                    {
                        Console.WriteLine("[ERRO]: Resposta da API está vazia");
                        normalizeValuesList = new Dictionary<string, string>();
                    }
                    else
                    {
                        Console.WriteLine($"[INFO]: Tamanho da resposta: {responseText.Length} caracteres");
                        var responseData = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(responseText);

                        foreach (var item in responseData!)
                        {
                            var jsonDebugPath = "/app/eCommerce/Server/Uploads/debug.json";
                            File.WriteAllText(jsonDebugPath, JsonConvert.SerializeObject(item, Formatting.Indented));
                            Console.WriteLine($"[DEBUG]: Item salvo em: {jsonDebugPath}");
                            //Console.WriteLine($"[DEBUG]: item: {JsonConvert.SerializeObject(item, Formatting.Indented)}");

                            if (item.TryGetValue("column", out var columnObj) && 
                                columnObj?.ToString() == value)
                            {
                                Console.WriteLine("[DEBUG]: Match de coluna encontrado!");

                                if (item.TryGetValue("from_to", out var fromToObj))
                                {
                                    Console.WriteLine($"[DEBUG]: from_to encontrado! Tipo: {fromToObj?.GetType().FullName}");

                                    Dictionary<string, object>? fromToDict = null;

                                    if (fromToObj is JObject jObj)
                                        fromToDict = jObj.ToObject<Dictionary<string, object>>();
                                    else if (fromToObj is Dictionary<string, object> dict)
                                        fromToDict = dict;
                                    else
                                        Console.WriteLine("[DEBUG]: Tipo de from_to não tratado");

                                    if (fromToDict != null)
                                    {
                                        foreach (var pair in fromToDict)
                                        {
                                            Console.WriteLine($"[DEBUG]: Normalizado: {pair.Key} => {pair.Value}");
                                            normalizeValuesList[pair.Key] = pair.Value?.ToString() ?? "";
                                        }

                                        CacheManagerLenovo.SaveToCache(value, normalizeValuesList, CACHE_FILE);
                                        return normalizeValuesList;
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("[DEBUG]: Campo 'fromTo' não encontrado");
                                }
                            }
                        }
                        Console.WriteLine("[ERRO]: Nenhum item correspondente encontrado na resposta da API.");
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