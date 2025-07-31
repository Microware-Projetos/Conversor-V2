using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace eCommerce.Server.Helpers;

public class CacheManager
{
    private const int CACHE_EXPIRY_DAYS = 300;
    
    public static void EnsureCacheDir(string cacheDir)
    {
        if (!Directory.Exists(cacheDir))
        {
            Directory.CreateDirectory(cacheDir);
        }
    }
    
    public static Dictionary<string, object> LoadCache(string cacheFile)
    {
        if (!File.Exists(cacheFile))
        {
            return new Dictionary<string, object>();
        }
        
        try
        {
            var json = File.ReadAllText(cacheFile);
            var cacheData = JsonConvert.DeserializeObject<Dictionary<string, CacheEntry>>(json) ?? new Dictionary<string, CacheEntry>();
            
            // Limpa itens expirados
            var currentTime = DateTime.Now;
            var validCache = new Dictionary<string, object>();
            
            foreach (var kvp in cacheData)
            {
                if (!string.IsNullOrEmpty(kvp.Value.Timestamp))
                {
                    var cacheTime = DateTime.Parse(kvp.Value.Timestamp);
                    if (currentTime - cacheTime < TimeSpan.FromDays(CACHE_EXPIRY_DAYS))
                    {
                        validCache[kvp.Key] = kvp.Value.Data;
                    }
                }
            }
            
            return validCache;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao carregar cache: {ex.Message}");
            return new Dictionary<string, object>();
        }
    }
    
    public static void SaveCache(string cacheFile, Dictionary<string, object> cacheData)
    {
        try
        {
            var cacheEntries = new Dictionary<string, CacheEntry>();
            foreach (var kvp in cacheData)
            {
                cacheEntries[kvp.Key] = new CacheEntry
                {
                    Data = kvp.Value,
                    Timestamp = DateTime.Now.ToString("O")
                };
            }
            
            var json = JsonConvert.SerializeObject(cacheEntries, Formatting.Indented);
            File.WriteAllText(cacheFile, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao salvar cache: {ex.Message}");
        }
    }
    
    public static object? GetCachedData(string sku, string cacheFile)
    {
        var cacheData = LoadCache(cacheFile);
        if (cacheData.ContainsKey(sku))
        {
            Console.WriteLine($"Usando cache para: {sku}");
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
    
    public class CacheEntry
    {
        public object? Data { get; set; }
        public string? Timestamp { get; set; }
    }
} 