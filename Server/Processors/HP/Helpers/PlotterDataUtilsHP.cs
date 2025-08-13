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
using WooAttribute = eCommerce.Shared.Models.Attribute;
using eCommerce.Server.Helpers;
using eCommerce.Shared.Models;
using eCommerce.Server.Processors.Lenovo;
using eCommerce.Server.Processors.Lenovo.Helpers;

namespace eCommerce.Server.Processors.HP.Helpers;

    public static class PlotterDataUtilsHP
    {
        private static readonly string CHACHE_DIR = "/app/eCommerce/Server/Cache/HP";
        private static string URL_PRODUCT = "https://partner.hp.com/o/headless-product-catalog/v1.0/product";
        private static string URL_GET_TOKEN = "https://partner.hp.com/o/oauth2/token";
        private static string HP_CLIENT_ID = "id-fcf82926-868e-acb2-ffa9-a63194f41d2";
        private static string HP_CLIENT_SECRET = "secret-4e78b957-e137-ccbe-15a2-92d1305c517b";
        private static string HP_COOKIE = "JSESSIONID=34127AEB1363FDAC3038ECEE125EA888.tomcatpfp1-g7t16160s; COOKIE_SUPPORT=true; GUEST_LANGUAGE_ID=en_US";
        private static readonly string TOKEN_PATH_FILE ="/app/eCommerce/Server/Cache/HP/tokenCache.json";
        private static readonly string PLOTTER_CACHE_FILE = "/app/eCommerce/Server/Cache/HP/plotterCache.json";

        private static readonly HttpClient _httpClient = new HttpClient();

        
        public static async Task<List<WooAttribute>> ProcessAttributes(IXLRow product)
        {
            var sku = product.Cell(2).Value.ToString() ?? "";
            var formatedSku = sku.Split('#')[0];

            if (string.IsNullOrEmpty(formatedSku))
            {
                Console.WriteLine($"[ERROR]: SKU vazio ou nulo na linha {product.RowNumber()}");
                return new List<WooAttribute>();
            }

            Console.WriteLine($"[INFO]: Processando atributos para o SKU: {formatedSku}");

            try
            {
                Console.WriteLine($"[INFO]: Verificando o Cache para o SKU: {formatedSku}");
                
                if (File.Exists(PLOTTER_CACHE_FILE))
                {
                    var cachedJson = File.ReadAllText(PLOTTER_CACHE_FILE);
                    var plotterData = JsonConvert.DeserializeObject<Dictionary<string, object>>(cachedJson) ?? new Dictionary<string, object>();

                    if (plotterData.ContainsKey(sku))
                    {
                        Console.WriteLine($"[INFO]: Dados do plotter encontrados no cache para o SKU: {formatedSku}");

                        // CRIAR LÓGICA DE RETORNO DE ATRIBUTOS WOOATTRIBUTIE
                        return new List<WooAttribute>();
                    }
                    else
                    {
                        Console.WriteLine($"[INFO]: Dados do plotter não encontrados no cache para o SKU: {formatedSku}");
                    }
                }
                else
                {
                    Console.WriteLine($"[INFO]: Cache não encontrado!");
                }

                string? accessToken = null;
                bool isTokenValid = CheckAccessTokenAsync();
                if(isTokenValid)
                {
                    Console.WriteLine("[INFO]: Access token válido encontrado.");
                    accessToken = GetSavedAccessToken();
                }
                else
                {
                    Console.WriteLine("[INFO]: Access token expirado ou não encontrado. Obtendo novo token...");
                    GetAccessTokenAsync().Wait();
                    accessToken = GetSavedAccessToken();

                    if (accessToken == null)
                    {
                        Console.WriteLine("[ERROR]: Não foi possível obter o access token.");
                        return new List<WooAttribute>();
                    }
                    else
                    {
                        Console.WriteLine($"[INFO]: Access token obtido: {accessToken}");
                    }
                }

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
                _httpClient.DefaultRequestHeaders.Add("Cookie", HP_COOKIE);
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                Console.WriteLine($"[INFO]: Processando atributos para SKU: {formatedSku}");
                
                var body = new
                {
                    oid = new string[] { },
                    sku = new[] { formatedSku },
                    reqContent = new[] { "plc", "chunks", "images" },
                    layoutName = "ALL-SPECS",
                    fallback = false,
                    requestor = "PFP-PRO",
                    apiserviceContext = new { pfpUserId = "1897480112" },
                    languageCode = "br",
                    countryCode = "BR"
                };

                var jsonBody = JsonConvert.SerializeObject(body);
                var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(URL_PRODUCT, content);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[ERROR]: Erro ao obter dados. Status: {response.StatusCode}");
                    return new List<WooAttribute>();
                }

                var responseContent = await response.Content.ReadAsStringAsync();

                var jsonObj = JObject.Parse(responseContent);
                var chunks = jsonObj["product"]?[0]?["chunks"];

                var plotterDataDict = new Dictionary<string, object>();

                if (File.Exists(PLOTTER_CACHE_FILE))
                {
                    var cachedJson = File.ReadAllText(PLOTTER_CACHE_FILE);
                    plotterDataDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(cachedJson) ?? new Dictionary<string, object>();
                    Console.WriteLine($"[INFO]: Dados do plotter carregados do cache.");
                }

                plotterDataDict[sku] = new
                {
                    chunks = chunks
                };
                
                
                string json = JsonConvert.SerializeObject(plotterDataDict, Formatting.Indented);
                File.WriteAllText(PLOTTER_CACHE_FILE,json);
                Console.WriteLine($"[INFO]: Dados do plotter salvos em {PLOTTER_CACHE_FILE}");

                var attributes = new List<WooAttribute>();

                //CRIAR LÓGICA PARA PROCESSAR OS DADOS DO PLOTTER E RETORNAR OS ATRIBUTOS

                Console.WriteLine($"[INFO]: Requisição realizada: {response.StatusCode}");
                return new List<WooAttribute>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR]: Erro ao processar atributos para o SKU {formatedSku}: {ex.Message}");
                return new List<WooAttribute>();
            }
        }
        
        public static async Task<List<MetaData>> ProcessPhotos(IXLRow product)
        {
            var sku = product.Cell(2).Value.ToString() ?? "";
            var formatedSku = sku.Split('#')[0];

            if (string.IsNullOrEmpty(formatedSku))
            {
                Console.WriteLine($"[ERROR]: SKU vazio ou nulo na linha {product.RowNumber()}");
                return new List<MetaData>();
            }

            Console.WriteLine($"[INFO]: Processando fotos para o SKU: {formatedSku}");

            try
            {
                // ----------------- 1. Verifica cache -----------------
                Dictionary<string, object> plotterData = new Dictionary<string, object>();
                var metaData = new List<MetaData>();
                CacheManagerHP.EnsureCacheDir(CHACHE_DIR);

                if (File.Exists(PLOTTER_CACHE_FILE))
                {
                    var cachedJson = File.ReadAllText(PLOTTER_CACHE_FILE);
                    plotterData = JsonConvert.DeserializeObject<Dictionary<string, object>>(cachedJson) ?? new Dictionary<string, object>();

                    if (plotterData.ContainsKey(sku))
                    {
                        Console.WriteLine($"[INFO]: Dados do plotter encontrados no cache para o SKU: {formatedSku}");

                        var jsonCacheObj = JObject.Parse(cachedJson);
                        var imagePlotter = jsonCacheObj[sku]?["images"] as JArray;
                        
                        if (imagePlotter == null || !imagePlotter.Any())
                        {
                            Console.WriteLine($"[INFO]: Nenhuma imagem encontrada para o SKU: {formatedSku}");
                        }
                        else
                        {
                            Console.WriteLine($"[INFO]: Imagens encontradas para o SKU: {formatedSku}");
                            
                            metaData.Add(new MetaData
                            {
                                key = "_external_image_url",
                                value = imagePlotter.First().ToString()
                            });

                            if (imagePlotter.Count > 1)
                            {
                                metaData.Add(new MetaData
                                {
                                    key = "_external_gallery_images",
                                    value = imagePlotter.Skip(1).ToList()
                                });
                            }

                            Console.WriteLine($"[INFO]: Total de imagens processadas: {metaData.Count}");
                            return metaData;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[INFO]: Dados do plotter não encontrados no cache para o SKU: {formatedSku}");
                    }
                }
                else
                {
                    Console.WriteLine($"[INFO]: Cache não encontrado!");
                    return new List<MetaData>();
                }
                
                // ----------------- 2. Requisição via API -----------------
                Console.WriteLine("[INFO]: Processando fotos via API...");

                metaData = await ProcessImagesAPI(product);

                Console.WriteLine($"[INFO]: Fotos processadas com sucesso para o SKU: {formatedSku}");

                return metaData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR]: Erro ao processar fotos para o SKU {formatedSku}: {ex.Message}");
                return new List<MetaData>();
            }
        }

        private static async Task<List<MetaData>> ProcessImagesAPI(IXLRow product)
        {
            var sku = product.Cell(2).Value.ToString() ?? "";
            var formatedSku = sku.Split('#')[0];

            string? accessToken = null;
            bool isTokenValid = CheckAccessTokenAsync();
            
            if(isTokenValid)
            {
                Console.WriteLine("[INFO]: Access token válido encontrado.");
                accessToken = GetSavedAccessToken();
            }
            else
            {
                Console.WriteLine("[INFO]: Access token expirado ou não encontrado. Obtendo novo token...");
                GetAccessTokenAsync().Wait();
                accessToken = GetSavedAccessToken();

                if (accessToken == null)
                {
                    throw new Exception("[ERROR]: Não foi possível obter o access token.");
                    return new List<MetaData>();
                }
                else
                {
                    Console.WriteLine($"[INFO]: Access token obtido: {accessToken}");
                }
            }

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
            _httpClient.DefaultRequestHeaders.Add("Cookie", HP_COOKIE);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var body = new
            {
                oid = new string[] { },
                sku = new[] { formatedSku },
                reqContent = new[] { "plc", "chunks", "images" },
                layoutName = "ALL-SPECS",
                fallback = false,
                requestor = "PFP-PRO",
                apiserviceContext = new { pfpUserId = "1897480112" },
                languageCode = "br",
                countryCode = "BR"
            };

            var jsonBody = JsonConvert.SerializeObject(body);
            var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(URL_PRODUCT, content);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[ERROR]: Erro ao obter dados. Status: {response.StatusCode}");
                return new List<MetaData>();
            }

            try
            {
                Console.WriteLine($"[INFO]: Requisição realizada: {response.StatusCode}");
                Console.WriteLine($"[INFO]: Processando imagens para o SKU: {formatedSku}");

                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonObj = JObject.Parse(responseContent);
                var images = jsonObj["product"]?[0]?["images"] as JArray;
                var resultImages = new List<string>();

                if (images != null)
                {
                    var orientationOrder = new[]
                    {
                        "Right",
                        "Other",
                        "Center",
                        "Other",
                        "Left"
                    };

                    var usadas = new HashSet<string>();

                    foreach (var orientation in orientationOrder)
                    {
                        var matchingImage = images
                            .SelectMany(img => img["details"] ?? Enumerable.Empty<JToken>())
                            .Where(details =>
                                details["background"]?.ToString() == "Transparent" &&
                                (int?)details["pixelHeight"] > 1000 &&
                                (int?)details["pixelWidth"] > 1000 &&
                                details["orientation"]?.ToString()
                                ?.StartsWith(orientation, StringComparison.OrdinalIgnoreCase) == true)
                            .Select(detail => detail["imageUrlHttps"]?.ToString())
                            .FirstOrDefault(url =>
                                !string.IsNullOrEmpty(url) && !usadas.Contains(url));
                        
                        if (!string.IsNullOrEmpty(matchingImage))
                        {
                            usadas.Add(matchingImage);

                            resultImages.Add(matchingImage.Split('?')[0]); // Remove query string
                            Console.WriteLine($"[INFO]: Imagem encontrada para a orientação '{orientation}': {matchingImage}");
                            if (resultImages.Count >= 5)
                            {
                                break;
                            }
                        }
                    }
                    // ----------------- Salva no cache -----------------
                    Dictionary<string, object> plotterData = new Dictionary<string, object>();
                    var metaData = new List<MetaData>();
                    CacheManagerHP.EnsureCacheDir(CHACHE_DIR);

                    if (File.Exists(PLOTTER_CACHE_FILE))
                    {
                        var cachedJson = File.ReadAllText(PLOTTER_CACHE_FILE);
                        plotterData = JsonConvert.DeserializeObject<Dictionary<string, object>>(cachedJson) ?? new Dictionary<string, object>();
                    }

                    if (!plotterData.ContainsKey(sku))
                    {
                        Console.WriteLine($"[INFO]: Adicionando novo SKU ao cache: {formatedSku}");
                        plotterData[sku] = new JObject();
                    }

                    var produto = plotterData[sku] as JObject ?? new JObject();
                    produto["images"] = JToken.FromObject(resultImages);
                    plotterData[sku] = produto;

                    File.WriteAllText(PLOTTER_CACHE_FILE, JsonConvert.SerializeObject(plotterData, Formatting.Indented));

                    metaData.Add(new MetaData
                    {
                        key = "_external_image_url",
                        value = resultImages.First()
                    });

                    if (resultImages.Count > 1)
                    {
                        metaData.Add(new MetaData
                        {
                            key = "_external_gallery_images",
                            value = resultImages.Skip(1).ToList()
                        });
                    }
                    return metaData;
                }
                else
                {
                    Console.WriteLine($"[INFO]: Nenhuma imagem encontrada para o SKU: {formatedSku}");
                    return new List<MetaData>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR]: Erro ao processar imagens para o SKU {formatedSku}: {ex.Message}");
                return new List<MetaData>();
            }
        }

        private static async Task GetAccessTokenAsync()
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

            try
            {
                var response = await _httpClient.PostAsync(URL_GET_TOKEN, content);
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
                    }
                }
                else
                {
                    Console.WriteLine("[ERROR]: Access token não encontrado na resposta.");
                }
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"[ERROR]: Problema na requisição: {e.Message}");
            }
            
        }

        private static string GetSavedAccessToken()
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

        private static string GetSavedDataExpiresToken()
        {
            if (File.Exists(TOKEN_PATH_FILE))
            {
                var json = File.ReadAllText(TOKEN_PATH_FILE);
                var tokenData = JsonConvert.DeserializeObject<dynamic>(json);
                return tokenData?.expires_in;
            }
            return null;
        }

        private static bool CheckAccessTokenAsync()
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
            return false;
        }

    }