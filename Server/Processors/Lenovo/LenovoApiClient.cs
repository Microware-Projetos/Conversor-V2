using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using eCommerce.Server.Helpers;
using eCommerce.Server.Processors.Lenovo;

namespace eCommerce.Server.Processors.Lenovo;

public class DataProcessorLenovo
{
    private const string CACHE_DIR = "Cache/Lenovo";
    private const string PRODUCT_CACHE_FILE = "Cache/Lenovo/product_cache.json";
    //private const string ATTRIBUTES_CACHE_FILE = "Cache/Lenovo/attributes_cache.json"; // Verificar se é necessário
    private static readonly HttpClient httpClient = new HttpClient();

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

        string hora = DateTime.Now.ToString("yyyyMMddHHmmss");
        
        // Se não estiver em cache, faz a chamada à API
        Console.WriteLine("[INFO]: Produto fora do Cache, chamando API");
        var url = $"https://psref.lenovo.com/api/model/Info/SpecData?model_code={sku}&t={hora}";
        
        try
        {
            HttpResponseMessage response = await httpClient.GetAsync(url);

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
                    CacheManagerLenovo.SaveToCache(sku, null, PRODUCT_CACHE_FILE);
                    return null;
                }

                CacheManagerLenovo.SaveToCache(sku, data, PRODUCT_CACHE_FILE);
                return data;
            }
            else
            {
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