using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using eCommerce.Server.Helpers;
using eCommerce.Server.Processors.Lenovo;
using eCommerce.Server.Processors.Lenovo.Helpers;

namespace eCommerce.Server.Processors.Lenovo.Helpers;

public class DataProcessorLenovo
{
    private const string CACHE_DIR = "Cache/Lenovo";
    private const string PRODUCT_CACHE_FILE = "Cache/Lenovo/product_cache.json";
    //private const string ATTRIBUTES_CACHE_FILE = "Cache/Lenovo/attributes_cache.json"; // Verificar se é necessário
    private static readonly HttpClient _httpClient = new HttpClient();

    public static async Task<object?> GetProductBySKU(string sku)
    {
        Console.WriteLine("[INFO]: Dentro de GetProductBySKU");
        // Garante que o diretório de cache existe
        CacheManagerLenovo.EnsureCacheDir(CACHE_DIR);

        // Verifica cache primeiro
        var cachedData = CacheManagerLenovo.GetCachedData(sku, PRODUCT_CACHE_FILE);
        //Console.WriteLine($"Vendo se o retorno dentro do GetCachedData dentro do GetProductBySKU é válido: {cachedData}");
        if (cachedData != null)
        {
            Console.WriteLine($"[INFO]: Pegando produto por SKU do Cache: {sku}");
            return cachedData;
        }
        
        // Se não estiver em cache, faz a chamada à API
        Console.WriteLine("[INFO]: Produto fora do Cache, chamando API");
        var url = $"https://psref.lenovo.com/api/model/Info/SpecData?model_code={sku}";
        string token = "eyJ0eXAiOiJKV1QifQ.bjVTdWk0YklZeUc2WnFzL0lXU0pTeU1JcFo0aExzRXl1UGxHN3lnS1BtckI0ZVU5WEJyVGkvaFE0NmVNU2U1ZjNrK3ZqTEVIZ29nTk1TNS9DQmIwQ0pTN1Q1VytlY1RpNzZTUldXbm4wZ1g2RGJuQWg4MXRkTmxKT2YrOW9LRjBzQUZzV05HM3NpcU92WFVTM0o0blM1SDQyUlVXNThIV1VBS2R0c1B2NjJyQjIrUGxNZ2x6RTRhUjY5UDZWclBX.ZDBmM2EyMWRjZTg2N2JmYWMxZDIxY2NiYjQzMWFhNjg1YjEzZTAxNmU2M2RmN2M5ZjIyZWJhMzZkOWI1OWJhZg";

        if (_httpClient.DefaultRequestHeaders.Contains("x-psref-user-token"))
            _httpClient.DefaultRequestHeaders.Remove("x-psref-user-token");

        _httpClient.DefaultRequestHeaders.Add("x-psref-user-token", token);

        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
        }
        
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            Console.WriteLine($"[INFO]: Response da chamada a api Lenovo em GetProductBySKU {response.StatusCode}");
            if(response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[INFO]: Resposta do body: {responseBody}");

                var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody);
                Console.WriteLine($"[INFO]: Conversor para objeto: {data}");
                
                if(data != null && data.TryGetValue("code", out var codeObj) &&
                    Convert.ToInt32(codeObj) == 0 &&
                    data.TryGetValue("msg", out var msgObj) &&
                    msgObj?.ToString() == "No corresponding product found")
                {
                    Console.WriteLine($"[INFO]: Atendeu ao If: data != null && data.TryGetValue('code', out var codeObj) && Convert.ToInt32(codeObj) == 0 && data.TryGetValue('msg', out var msgObj) && msgObj?.ToString() == 'No corresponding product found'");
                    CacheManagerLenovo.SaveToCache(sku, null, PRODUCT_CACHE_FILE);
                    return null;
                }

                Console.WriteLine("[INFO]: Final do IsSuccessStatusCode");
                CacheManagerLenovo.SaveToCache(sku, data, PRODUCT_CACHE_FILE);
                return data;
            }
            else
            {
                Console.WriteLine("[INFO]: Else do chamado a API GetProductBySKU");
                CacheManagerLenovo.SaveToCache(sku, null, PRODUCT_CACHE_FILE);
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO]: Erro ao consultar SKU: {sku}: {ex.Message}");
            CacheManagerLenovo.SaveToCache(sku, null, PRODUCT_CACHE_FILE);
            return null;
        }
    }
}