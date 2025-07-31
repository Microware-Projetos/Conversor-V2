using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using eCommerce.Server.Helpers;

namespace eCommerce.Server.Processors.Lenovo;

public class DataProcessorLenovo
{
    private const string CACHE_DIR = "Cache/Lenovo";
    private const string PRODUCT_CACHE_FILE = "Cache/Lenovo/product_cache.json";
    //private const string ATTRIBUTES_CACHE_FILE = "Cache/Lenovo/attributes_cache.json"; // Verificar se é necessário
    private static readonly HttpClient httpClient = new HttpClient();

    public static async Task<object?> GetProductBySKU(string sku)
    {
        // Garante que o diretório de cache existe
        CacheManager.EnsureCacheDir(CACHE_DIR);

        // Verifica cache primeiro
        var cachedData = CacheManager.GetCachedData(sku, PRODUCT_CACHE_FILE);
        if (cachedData != null)
        {
            return cachedData;
        }

        string hora = DateTime.Now.ToString("yyyyMMddHHmmss");
        
        // Se não estiver em cache, faz a chamada à API
        var url = $"https://psref.lenovo.com/api/model/Info/SpecData?model_code={sku}&t={hora}";
        
        try
        {
            HttpResponseMessage response = await httpClient.GetAsync(url);

            if(response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<object>(responseBody);
                
                if(productData != null && productData.TryGetValue("code", out var codeObj) &&
                    Convert.ToInt32(codeObj) == 0 &&
                    productData.TryGetValue("msg", out var msgObj) &&
                    msgObj?.ToString() == "No corresponding product found")
                {
                    CacheManager.SaveToCache(sku, null, PRODUCT_CACHE_FILE);
                    return null;
                }

                CacheManager.SaveToCache(sku, data, PRODUCT_CACHE_FILE);
                return data;
            }
            else
            {
                CacheManager.SaveToCache(sku, null, PRODUCT_CACHE_FILE);
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao consultar SKU: {sku}: {ex.Message}");
            CacheManager.SaveToCache(sku, null, PRODUCT_CACHE_FILE);
            return null;
        }
    }
}