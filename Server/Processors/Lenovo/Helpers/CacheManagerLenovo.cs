using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace eCommerce.Server.Processors.Lenovo.Helpers;

public class CacheManagerLenovo
{
    private const int CACHE_EXPIRY_DAYS = 300;
    public static Dictionary<string, object> ProductCache { get; private set; } = new();
    
    public static void EnsureCacheDir(string cacheDir)
    {
        if (!Directory.Exists(cacheDir))
        {
            Directory.CreateDirectory(cacheDir);
        }
    }
    
    public static Dictionary<string, object> LoadCache(string cacheFile)
    {
        Console.WriteLine($"[INFO] Iniciando LoadCache para o arquivo: {cacheFile}");
        if (!File.Exists(cacheFile))
        {
            Console.WriteLine("[WARNING] Arquivo de cache não encontrado.");
            return new Dictionary<string, object>();
        }
        
        try
        {
            string json = File.ReadAllText(cacheFile);
            ProductCache = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            Console.WriteLine($"[INFO] Cache carregado!");

            return ProductCache;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO] Não foi possível carregar o cache: {ex.Message}");
            return ProductCache = new Dictionary<string, object>();
        }
    }
    
    public static void SaveCache(string cacheFile, Dictionary<string, object> cacheData)
    {
        try
        {
            var json = JsonConvert.SerializeObject(cacheData, Formatting.Indented);
            File.WriteAllText(cacheFile, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao salvar cache: {ex.Message}");
        }
    }
    
    public static object? GetCachedData(string sku, string cacheFile)
    {
        Console.WriteLine("[INFO]: Dentro de GetCachedData");
        var cacheData = LoadCache(cacheFile);
        if (cacheData.ContainsKey(sku))
        {
            Console.WriteLine($"[INFO]: Usando cache para: {sku}");
            //Console.WriteLine($"[INFO]: Retorno do GetCachedData: {cacheData[sku]}");
            return cacheData[sku];
        }
        return null;
    }
    
    public static void SaveToCache(string sku, object data, string cacheFile)
    {
        var cacheData = LoadCache(cacheFile);
        cacheData[sku] = data;
        SaveCache(cacheFile, cacheData);
    }
    
} 