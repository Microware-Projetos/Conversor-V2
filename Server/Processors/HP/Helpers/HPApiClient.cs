using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using eCommerce.Server.Helpers;
using eCommerce.Server.Processors;

namespace eCommerce.Server.Processors.HP.Helpers;

public class DataProcessorHP
{
    private const string PRODUCT_CACHE_FILE = "Cache/HP/product_cache.json";
    private const string ATTRIBUTES_CACHE_FILE = "Cache/HP/attributes_cache.json";
    
    private static readonly HttpClient httpClient = new HttpClient();
    
    public static async Task<object?> GetAttributesBySKU(string sku)
    {
        sku = sku.Split('#')[0]; // Remove parte após #
        
        // Verifica cache primeiro
        var cachedData = CacheManagerHP.GetCachedData(sku, ATTRIBUTES_CACHE_FILE);
        if (cachedData != null)
        {
            return cachedData;
        }
        
        // Se não estiver em cache, faz a chamada à API
        var url = $"https://support.hp.com/wcc-services/pdp/specifications/br-pt?authState=anonymous&template=ProductModel&sku={sku}";
        
        try
        {
            var response = await httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<object>(json);
                
                // Salva no cache
                if (data != null)
                {
                    CacheManagerHP.SaveToCache(sku, data, ATTRIBUTES_CACHE_FILE);
                }
                return data;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro na API de atributos: {ex.Message}");
        }
        
        return null;
    }
    
    public static async Task<object?> GetProductBySKU(string sku)
    {
        sku = sku.Split('#')[0]; // Remove parte após #
        
        // Verifica cache primeiro
        var cachedData = CacheManagerHP.GetCachedData(sku, PRODUCT_CACHE_FILE);
        if (cachedData != null)
        {
            return cachedData;
        }
        
        // Se não estiver em cache, faz a chamada à API
        var url = "https://support.hp.com/wcc-services/profile/devices/warranty/specs?cache=true&authState=anonymous&template=ProductModel";
        var body = new
        {
            cc = "br",
            lc = "pt",
            utcOffset = "M0300",
            customerId = "",
            deviceId = 1234,
            devices = new[]
            {
                new
                {
                    seriesOid = (object?)null,
                    modelOid = (object?)null,
                    serialNumber = (object?)null,
                    productNumber = sku
                }
            },
            captchaToken = ""
        };
        
        try
        {
            var jsonBody = JsonConvert.SerializeObject(body);
            var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(url, content);
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<object>(json);
                
                // Salva no cache
                if (data != null)
                {
                    CacheManagerHP.SaveToCache(sku, data, PRODUCT_CACHE_FILE);
                }
                return data;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro na API de produto: {ex.Message}");
        }
        
        return null;
    }

} 