using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Threading;
using System.Net.Http.Headers;
using System.Linq;

namespace eCommerce.Server.Processors.HP.Helpers;

public class CacheManagerHP
{
    private const int CACHE_EXPIRY_DAYS = 300;
    private static string URL_GET_TOKEN = "https://partner.hp.com/o/oauth2/token";
    private static string HP_CLIENT_ID = "id-fcf82926-868e-acb2-ffa9-a63194f41d2";
    private static string HP_CLIENT_SECRET = "secret-4e78b957-e137-ccbe-15a2-92d1305c517b";
    private static string HP_COOKIE = "JSESSIONID=34127AEB1363FDAC3038ECEE125EA888.tomcatpfp1-g7t16160s; COOKIE_SUPPORT=true; GUEST_LANGUAGE_ID=en_US";
    private static readonly string TOKEN_PATH_FILE ="/app/eCommerce/Server/Cache/HP/tokenCache.json";
    private static readonly HttpClient _httpClient = new HttpClient()
    {
        Timeout = TimeSpan.FromMinutes(2)
    };
    
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

    public static async Task GetAccessTokenAsync()
    {
        Console.WriteLine($"[INFO]: Obtendo novo access token de {URL_GET_TOKEN}");

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", HP_CLIENT_ID ?? ""),
            new KeyValuePair<string, string>("client_secret", HP_CLIENT_SECRET ?? "")
        });

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
        _httpClient.DefaultRequestHeaders.Add("Cookie", HP_COOKIE);

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                Console.WriteLine($"[INFO]: Tentativa {attempt} de obter token...");

                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                var response = await _httpClient.PostAsync(URL_GET_TOKEN, content, cts.Token);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();

                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("access_token", out var tokenElement))
                {
                    string? accessToken = tokenElement.GetString();

                    if(accessToken != null)
                    {
                        var tokenData = new 
                        {
                            access_token = accessToken,
                            generated_at = DateTime.UtcNow,
                            expires_in = DateTime.UtcNow.AddMinutes(10)
                        };

                        var jsonToSave = JsonConvert.SerializeObject(tokenData, Formatting.Indented);
                        File.WriteAllText(TOKEN_PATH_FILE, jsonToSave);
                        Console.WriteLine($"[INFO]: Access token obtido e salvo com sucesso");
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("[ERROR]: Access token não encontrado na resposta.");
                }
            }
            catch (TaskCanceledException ex)
            {
                if (attempt == 3)
                {
                    System.Console.WriteLine("[ERROR]: Token timeout: Tentativa 3 de 3");
                    throw;
                }
                await Task.Delay(2000 * attempt);
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"[ERROR]: Tentativa {attempt} falhou: {e.Message}");
                if (attempt == 3)
                {
                    throw;
                }
                await Task.Delay(2000 * attempt);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR]: Erro inesperado na tentativa {attempt}: {ex.Message}");
                if (attempt == 3)
                {
                    throw;
                }
                await Task.Delay(2000 * attempt);
            }
        } 
    }

    public static string GetSavedAccessToken()
    {
        Console.WriteLine($"[INFO]: Verificando token salvo em: {TOKEN_PATH_FILE}");
        if (File.Exists(TOKEN_PATH_FILE))
        {
            var json = File.ReadAllText(TOKEN_PATH_FILE);
            var tokenData = JsonConvert.DeserializeObject<dynamic>(json);
            Console.WriteLine($"[INFO]: Token encontrado no arquivo.");
            return tokenData?.access_token;
        }
        Console.WriteLine("[INFO]: Nenhum token salvo encontrado.");
        return null;
    }

    public static string GetSavedDataExpiresToken()
    {
        if (File.Exists(TOKEN_PATH_FILE))
        {
            var json = File.ReadAllText(TOKEN_PATH_FILE);
            var tokenData = JsonConvert.DeserializeObject<dynamic>(json);
            return tokenData?.expires_in;
        }
        return null;
    }

    public static bool CheckAccessTokenAsync()
    {
        var expiresDataToken = GetSavedDataExpiresToken();
        
        if (expiresDataToken == null)
        {
            Console.WriteLine("[ERROR]: Token expirado ou não encontrado.");
            return false;
        }
        
        if (DateTime.Parse(expiresDataToken) > DateTime.UtcNow)
        {
            Console.WriteLine($"[INFO]: Access token valido até {expiresDataToken}.");
            return true;
        }

        Console.WriteLine("[INFO]: Access token expirado.");
        File.WriteAllText(TOKEN_PATH_FILE, "");
        return false;
    }
    
    public class CacheEntry
    {
        public object? Data { get; set; }
        public string? Timestamp { get; set; }
    }
} 