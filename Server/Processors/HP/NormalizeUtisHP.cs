using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace eCommerce.Server.Helpers;

public static class NormalizeUtisHP
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private static readonly Dictionary<string, Dictionary<string, string>> _normalizedValuesCache = new();
    
    public static async Task<List<object>> BuscarImagens()
    {
        try
        {
            var url = "https://eprodutos-integracao.microware.com.br/api/photos/allId";
            var response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var jsonArray = JArray.Parse(content);
                return jsonArray.ToObject<List<object>>();
            }
            
            Console.WriteLine($"Erro ao buscar imagens: Status {response.StatusCode}");
            return new List<object>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao buscar imagens: {ex.Message}");
            return new List<object>();
        }
    }
    
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
        
        Console.WriteLine($"Buscando valores de {value} na API...");
        Dictionary<string, string> normalizeValuesList = new();
        
        // Valores padrão para fallback
        var defaultValues = new Dictionary<string, Dictionary<string, string>>
        {
            ["Familia"] = new Dictionary<string, string>
            {
                ["Notebook"] = "Notebook",
                ["Desktop"] = "Desktop",
                ["Workstation"] = "Workstation",
                ["Thin Client"] = "Thin Client",
                ["Display"] = "Display",
                ["Acessório"] = "Acessório"
            },
            ["Anatel"] = new Dictionary<string, string>
            {
                ["Notebook"] = "ANATEL: 12345",
                ["Desktop"] = "ANATEL: 12346",
                ["Workstation"] = "ANATEL: 12347",
                ["Thin Client"] = "ANATEL: 12348",
                ["Display"] = "ANATEL: 12349",
                ["Acessório"] = "ANATEL: 12350"
            }
        };
        
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
                        normalizeValuesList = defaultValues.GetValueOrDefault(value, new Dictionary<string, string>());
                    }
                    else
                    {
                        Console.WriteLine($"Tamanho da resposta: {responseText.Length} caracteres");
                        
                        // Encontra o início do objeto que contém os valores que queremos
                        var startMarker = $"\"column\":\"{value}\",\"from_to\":";
                        var startPos = responseText.IndexOf(startMarker);
                        
                        if (startPos != -1)
                        {
                            Console.WriteLine($"Encontrado início dos valores para {value}");
                            // Encontra o fim do objeto
                            startPos += startMarker.Length;
                            var braceCount = 1;
                            var endPos = startPos;
                            
                            while (braceCount > 0 && endPos < responseText.Length)
                            {
                                if (responseText[endPos] == '{')
                                    braceCount++;
                                else if (responseText[endPos] == '}')
                                    braceCount--;
                                endPos++;
                            }
                            
                            if (braceCount == 0)
                            {
                                // Extrai apenas o objeto from_to
                                var fromToText = responseText.Substring(startPos, endPos - startPos - 1);
                                Console.WriteLine($"Texto extraído: {fromToText}");
                                
                                try
                                {
                                    // Tenta fazer o parse do JSON extraído
                                    var fromToObject = JObject.Parse(fromToText);
                                    normalizeValuesList = fromToObject.ToObject<Dictionary<string, string>>();
                                    Console.WriteLine($"Valores extraídos com sucesso para {value}");
                                }
                                catch (JsonException ex)
                                {
                                    Console.WriteLine($"Erro ao decodificar objeto from_to: {ex.Message}");
                                    Console.WriteLine($"Texto extraído: {fromToText}");
                                    
                                    // Tenta uma abordagem alternativa - procura por pares chave-valor
                                    try
                                    {
                                        var alternativeDict = new Dictionary<string, string>();
                                        var lines = fromToText.Split(',');
                                        foreach (var line in lines)
                                        {
                                            var cleanLine = line.Trim().Trim('"', '{', '}');
                                            if (cleanLine.Contains(':'))
                                            {
                                                var parts = cleanLine.Split(':', 2);
                                                if (parts.Length == 2)
                                                {
                                                    var key = parts[0].Trim().Trim('"');
                                                    var val = parts[1].Trim().Trim('"');
                                                    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(val))
                                                    {
                                                        alternativeDict[key] = val;
                                                    }
                                                }
                                            }
                                        }
                                        
                                        if (alternativeDict.Count > 0)
                                        {
                                            normalizeValuesList = alternativeDict;
                                            Console.WriteLine($"Valores extraídos com método alternativo para {value}");
                                        }
                                        else
                                        {
                                            Console.WriteLine("Método alternativo também falhou, usando valores padrão");
                                            normalizeValuesList = defaultValues.GetValueOrDefault(value, new Dictionary<string, string>());
                                        }
                                    }
                                    catch
                                    {
                                        Console.WriteLine("Usando valores padrão como fallback");
                                        normalizeValuesList = defaultValues.GetValueOrDefault(value, new Dictionary<string, string>());
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine("Não foi possível encontrar o fim do objeto");
                                Console.WriteLine("Usando valores padrão como fallback");
                                normalizeValuesList = defaultValues.GetValueOrDefault(value, new Dictionary<string, string>());
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Não foi possível encontrar os valores para {value}");
                            Console.WriteLine("Usando valores padrão como fallback");
                            normalizeValuesList = defaultValues.GetValueOrDefault(value, new Dictionary<string, string>());
                        }
                        
                        // Armazena no cache
                        _normalizedValuesCache[value] = normalizeValuesList;
                        Console.WriteLine($"Total de valores normalizados: {normalizeValuesList.Count}");
                        Console.WriteLine("Valores armazenados no cache");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nERRO ao processar resposta da API:");
                    Console.WriteLine($"Tipo do erro: {ex.GetType().Name}");
                    Console.WriteLine($"Mensagem do erro: {ex.Message}");
                    Console.WriteLine("Usando valores padrão como fallback");
                    normalizeValuesList = defaultValues.GetValueOrDefault(value, new Dictionary<string, string>());
                    _normalizedValuesCache[value] = normalizeValuesList;
                }
            }
            else
            {
                Console.WriteLine($"ERRO na API: Status {(int)request.StatusCode}");
                Console.WriteLine("Usando valores padrão como fallback");
                normalizeValuesList = defaultValues.GetValueOrDefault(value, new Dictionary<string, string>());
                _normalizedValuesCache[value] = normalizeValuesList;
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"\nERRO na requisição à API:");
            Console.WriteLine($"Tipo do erro: {ex.GetType().Name}");
            Console.WriteLine($"Mensagem do erro: {ex.Message}");
            Console.WriteLine("Usando valores padrão como fallback");
            normalizeValuesList = defaultValues.GetValueOrDefault(value, new Dictionary<string, string>());
            _normalizedValuesCache[value] = normalizeValuesList;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nERRO inesperado na normalização:");
            Console.WriteLine($"Tipo do erro: {ex.GetType().Name}");
            Console.WriteLine($"Mensagem do erro: {ex.Message}");
            Console.WriteLine("Usando valores padrão como fallback");
            normalizeValuesList = defaultValues.GetValueOrDefault(value, new Dictionary<string, string>());
            _normalizedValuesCache[value] = normalizeValuesList;
        }
        
        return normalizeValuesList;
    }
}
