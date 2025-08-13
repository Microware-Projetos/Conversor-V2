using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace eCommerce.Server.Processors.HP.Helpers;

public class CacheManagerHP
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
        Console.WriteLine($"[INFO] Iniciando LoadCache para o arquivo: {cacheFile}");
        if (!File.Exists(cacheFile))
        {
            Console.WriteLine("[AVISO] Arquivo de cache não encontrado.");
            return new Dictionary<string, object>();
        }
        
        try
        {
            Console.WriteLine("[INFO] Lendo conteúdo do arquivo...");
            var json = File.ReadAllText(cacheFile);
            Console.WriteLine("[DEBUG] Conteúdo JSON lido:");
            var cacheData = JsonConvert.DeserializeObject<Dictionary<string, CacheEntry>>(json) ?? new Dictionary<string, CacheEntry>();
            Console.WriteLine($"[INFO] Total de entradas no cache lido: {cacheData.Count}");
            
            // Limpa itens expirados
            var currentTime = DateTime.Now;
            var validCache = new Dictionary<string, object>();
            int expirados = 0, validos = 0;
            
            foreach (var kvp in cacheData)
            {
                if (!string.IsNullOrEmpty(kvp.Value.Timestamp))
                {
                    var cacheTime = DateTime.Parse(kvp.Value.Timestamp);
                    if (currentTime - cacheTime < TimeSpan.FromDays(CACHE_EXPIRY_DAYS))
                    {
                        validCache[kvp.Key] = kvp.Value.Data;
                        validos++;
                    }
                    else
                    {
                        expirados++;
                        Console.WriteLine($"[INFO] Item expirado: {kvp.Key} (timestamp: {kvp.Value.Timestamp})");
                    }
                }
                else
                {
                    Console.WriteLine($"[AVISO] Item sem timestamp: {kvp.Key}");
                }
            }
            Console.WriteLine($"[INFO] Cache válido carregado com sucesso. Total: {validos} válidos, {expirados} expirados.");
            return validCache;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO] Falha ao carregar cache: {ex.Message}");
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
            Console.WriteLine($"[INFO]: Usando cache para: {sku}");
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

    public static object? GetCachedDataPlotter(string sku, string cacheFile)
    {
        var cacheData = LoadCache(cacheFile);
        if (cacheData.ContainsKey(sku))
        {
            Console.WriteLine($"[INFO]: Usando cache para plotter: {sku}");
            return cacheData[sku];
        }
        return null;
    }
    
    public class CacheEntry
    {
        public object? Data { get; set; }
        public string? Timestamp { get; set; }
    }
} 