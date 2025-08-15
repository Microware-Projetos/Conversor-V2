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
using eCommerce.Server.Processors.HP.Helpers;

namespace eCommerce.Server.Processors.HP.Helpers;

    public static class PlotterDataUtilsHP
    {
        private static readonly string CHACHE_DIR = "/app/eCommerce/Server/Cache/HP";
        private static string URL_PRODUCT = "https://partner.hp.com/o/headless-product-catalog/v1.0/product";
        private static string HP_COOKIE = "JSESSIONID=34127AEB1363FDAC3038ECEE125EA888.tomcatpfp1-g7t16160s; COOKIE_SUPPORT=true; GUEST_LANGUAGE_ID=en_US";
        private static readonly string PLOTTER_CACHE_FILE = "/app/eCommerce/Server/Cache/HP/plotterCache.json";
        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task<List<WooAttribute>> ProcessAttributes(string sku)
        {
            var formatedSku = sku.Split('#')[0];

            if (string.IsNullOrEmpty(formatedSku))
            {
                Console.WriteLine($"[ERROR]: SKU vazio ou nulo");
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

                        System.Console.WriteLine($"[INFO]: Atributos processados no cache para o SKU: {formatedSku}");
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

                Console.WriteLine("[INFO]: Processando atributos via API...");
                var attributes = await ProcessAttributesApi(sku);
                
                
                if(attributes == null || !attributes.Any())
                {
                    Console.WriteLine($"[INFO]: Nenhum atributo encontrado para o SKU: {formatedSku}");
                    return new List<WooAttribute>();
                }

                Console.WriteLine($"[INFO]: Atributos processados com sucesso para o SKU: {formatedSku}");
                return attributes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR]: Erro ao processar atributos para o SKU {formatedSku}: {ex.Message}");
                return new List<WooAttribute>();
            }
        }
        
        public static async Task<List<WooAttribute>> ProcessAttributesApi(string sku)
        {            
            var formatedSku = sku.Split('#')[0];
            string? accessToken = null;
            
            if (string.IsNullOrEmpty(formatedSku))
            {
                Console.WriteLine($"[ERROR]: SKU vazio ou nulo na linha");
                return new List<WooAttribute>();
            }

            Console.WriteLine($"[INFO]: Processando atributos via API para o SKU: {formatedSku}");

            try
            {
                bool isTokenValid = CacheManagerHP.CheckAccessTokenAsync();
                if(isTokenValid)
                {
                    Console.WriteLine("[INFO]: Access token válido encontrado.");
                    accessToken = CacheManagerHP.GetSavedAccessToken();
                }
                else
                {
                    Console.WriteLine("[INFO]: Access token expirado ou não encontrado. Obtendo novo token...");
                    CacheManagerHP.GetAccessTokenAsync().Wait();
                    accessToken = CacheManagerHP.GetSavedAccessToken();

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
                                
                var plotterDataDict = new Dictionary<string, object>();

                var detailsDict = new Dictionary<string, string>();
                var atributoEncontrado = false;

                Console.WriteLine($"[INFO]: Buscando atributos para o SKU: {formatedSku}");

                try
                {
                    foreach (var detail in jsonObj["product"]?
                        .SelectMany(p => p["chunks"])
                        .SelectMany(c => c["details"] ?? Enumerable.Empty<JToken>()))
                    {
                        var name = detail["tag"]?.ToString();
                        var value = detail["value"]?.ToString();

                        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
                        {
                            System.Console.WriteLine($"[INFO]: Atributo encontrado: {name}");
                            if (!detailsDict.ContainsKey(name)) // mantém só o primeiro que aparecer
                            {
                                detailsDict[name] = value;
                            }
                            else
                                Console.WriteLine($"[INFO]: Atributo já presente para: {name}");
                        }
                        else
                        {
                            Console.WriteLine($"[INFO]: Atributo não encontrado!");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARNING]: Atributos não encontrados para o SKU: {formatedSku}");

                    var cachedJson = File.ReadAllText(PLOTTER_CACHE_FILE);
                    plotterDataDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(cachedJson) ?? new Dictionary<string, object>();
                    plotterDataDict[sku] = null;
                    string newJson = JsonConvert.SerializeObject(plotterDataDict, Formatting.Indented);
                    File.WriteAllText(PLOTTER_CACHE_FILE, newJson);
                    Console.WriteLine($"[INFO]: Dados do plotter salvos como NULL em {PLOTTER_CACHE_FILE}");

                    return new List<WooAttribute>();
                }

                if (File.Exists(PLOTTER_CACHE_FILE))
                {
                    var cachedJson = File.ReadAllText(PLOTTER_CACHE_FILE);
                    plotterDataDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(cachedJson) ?? new Dictionary<string, object>();
                    Console.WriteLine($"[INFO]: Dados do plotter carregados do cache.");
                }

                Dictionary<string, string> existingDetails;

                if (plotterDataDict.ContainsKey(sku))
                {
                    existingDetails = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                        plotterDataDict[sku].ToString()
                    ) ?? new Dictionary<string, string>();
                }
                else
                {
                    existingDetails = new Dictionary<string, string>();
                }

                foreach (var kvp in detailsDict)
                {
                    if(!existingDetails.ContainsKey(kvp.Key))
                        existingDetails[kvp.Key] = kvp.Value;
                }

                plotterDataDict[sku] = existingDetails;

                string json = JsonConvert.SerializeObject(plotterDataDict, Formatting.Indented);
                File.WriteAllText(PLOTTER_CACHE_FILE, json);
                Console.WriteLine($"[INFO]: Dados do plotter salvos em {PLOTTER_CACHE_FILE}");

                Console.WriteLine($"[INFO]: Requisição realizada: {response.StatusCode}");
                return new List<WooAttribute>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR]: Erro ao processar atributos por API para o SKU {formatedSku}: {ex.Message}");
                return new List<WooAttribute>();
            }
        }

        public static async Task<List<MetaData>> ProcessPhotos(string sku)
        {
            var formatedSku = sku.Split('#')[0];

            if (string.IsNullOrEmpty(formatedSku))
            {
                Console.WriteLine($"[ERROR]: SKU vazio ou nulo na linha");
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
                metaData = await ProcessImagesAPI(sku);

                if (metaData == null || !metaData.Any())
                {
                    Console.WriteLine($"[INFO]: Nenhuma foto encontrada para o SKU na API: {formatedSku}");
                    return new List<MetaData>();
                }

                Console.WriteLine($"[INFO]: Fotos processadas com sucesso para o SKU: {formatedSku}");
                return metaData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR]: Erro ao processar fotos para o SKU {formatedSku}: {ex.Message}");
                return new List<MetaData>();
            }
        }

        public static string ProcessWeight(string sku)
        {
            CacheManagerHP.EnsureCacheDir(CHACHE_DIR);
            string DEFAULT_WEIGHT = "130.6";

            try
            {
                if(File.Exists(PLOTTER_CACHE_FILE))
                {
                    var cachedJson = File.ReadAllText(PLOTTER_CACHE_FILE);
                    var plotterData = JsonConvert.DeserializeObject<Dictionary<string, object>>(cachedJson) ?? new Dictionary<string, object>();
                    var weight = string.Empty;

                    if (plotterData.ContainsKey(sku))
                    {
                        Console.WriteLine($"[INFO]: Dados do plotter encontrados no cache para o SKU: {sku}");
                        var jsonCacheObj = JObject.Parse(cachedJson);

                        if(jsonCacheObj[sku]?["weightpackmet"] != null)
                        {
                            weight = jsonCacheObj[sku]?["weightpackmet"]?.ToString();
                            Console.WriteLine($"[INFO]: Peso com Embalagem encontrado no cache: {weight}");
                        }
                        else if(jsonCacheObj[sku]?["weightmet"] != null)
                        {
                            weight = jsonCacheObj[sku]?["weightmet"]?.ToString();
                            Console.WriteLine($"[INFO]: Peso encontrado no cache: {weight}");
                        }

                        if (!string.IsNullOrEmpty(weight))
                        {
                            Console.WriteLine($"[INFO]: Convertendo peso para kg: {weight}");
                            weight = ConvertToKg(weight, sku);

                            return weight;
                        }
                        else
                        {
                            Console.WriteLine($"[INFO]: Peso não encontrado no cache para o SKU: {sku}");
                            return DEFAULT_WEIGHT;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[INFO]: Dados do plotter não encontrados no cache para o SKU: {sku}");
                        return DEFAULT_WEIGHT;
                    }
                }
                else
                {
                    Console.WriteLine($"[INFO]: Cache não encontrado!");
                    return DEFAULT_WEIGHT;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR]: Erro ao processar peso para o SKU {sku}: {ex.Message}");
                return DEFAULT_WEIGHT;
            }
        }

        public static Dimensions ProcessDimensions(string sku)
        {
            Dimensions DEFAULT_DIMENSIONS = new Dimensions { length = "176.2", width = "69.8", height = "102.0" };
            
            if (string.IsNullOrEmpty(sku))
            {
                Console.WriteLine($"[ERROR]: SKU vazio ou nulo na linha");
                Console.WriteLine("[WARNING]: Aplicando dimensão default");
                
                return DEFAULT_DIMENSIONS;
            }

            try
            {
                if(File.Exists(PLOTTER_CACHE_FILE))
                {
                    var cachedJson = File.ReadAllText(PLOTTER_CACHE_FILE);
                    var plotterData = JsonConvert.DeserializeObject<Dictionary<string, object>>(cachedJson) ?? new Dictionary<string, object>();

                    if(plotterData.ContainsKey(sku))
                    {
                        Console.WriteLine($"[INFO]: Dados do plotter encontrados no cache para o SKU: {sku}");
                        var jsonCacheObj = JObject.Parse(cachedJson);

                        if(jsonCacheObj[sku]?["dimenpackmet"] != null)
                        {
                            var dimensions = jsonCacheObj[sku]?["dimenpackmet"]?.ToString().Replace(" ", "");
                            Console.WriteLine($"[INFO]: Dimensões com Embalagem encontradas no cache: {dimensions}");
                            Dictionary<string, string> dimensionsDict = new Dictionary<string, string>();
                            Console.WriteLine($"[INFO]: Processando dimensões para o SKU: {sku}");
                            dimensionsDict["length"] = ExtractDimension(dimensions, 0); //dimensions.Split("x")[0];
                            dimensionsDict["width"] = ExtractDimension(dimensions, 1); //dimensions.Split("x")[1];
                            dimensionsDict["height"] = ExtractDimension(dimensions, 2); //dimensions.Split("x")[2];
                            
                            return new Dimensions
                            {
                                length = dimensionsDict["length"],
                                width = dimensionsDict["width"],
                                height = dimensionsDict["height"]
                            };
                            
                        }
                        else
                        {
                            Console.WriteLine($"[INFO]: Dimensões não encontradas para o SKU: {sku}");
                            Console.WriteLine("[WARNING]: Aplicando dimensão default");
                            
                            return DEFAULT_DIMENSIONS;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[INFO]: Dados do plotter não encontrados no cache para o SKU: {sku}");
                        Console.WriteLine("[WARNING]: Aplicando dimensão default");
                        return DEFAULT_DIMENSIONS;
                    }
                }
                else
                {
                    Console.WriteLine($"[INFO]: Cache não encontrado!");
                    Console.WriteLine("[WARNING]: Aplicando dimensão default");

                    return DEFAULT_DIMENSIONS;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR]: Erro ao processar dimensões para o SKU {sku}: {ex.Message}");
                Console.WriteLine("[WARNING]: Aplicando dimensão default");

                return DEFAULT_DIMENSIONS;
            }
        }

        public static string ProcessShippingClass(IXLRow linha)
        {
            try
            {
                var icmsSP = linha.Cell(9).Value.ToString();

                Console.WriteLine($"[INFO]: ICMS SP: {icmsSP}");

                switch(icmsSP)
                {
                    case "0.04":
                        return "importado";
                    default:
                        return "local";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR]: Erro ao processar shipping class: {ex.Message}");
                return string.Empty;
            }
        }

        private static async Task<List<MetaData>> ProcessImagesAPI(string sku)
        {
            var formatedSku = sku.Split('#')[0];

            string? accessToken = null;
            bool isTokenValid = CacheManagerHP.CheckAccessTokenAsync();
            
            if(isTokenValid)
            {
                Console.WriteLine("[INFO]: Access token válido encontrado.");
                accessToken = CacheManagerHP.GetSavedAccessToken();
            }
            else
            {
                Console.WriteLine("[INFO]: Access token expirado ou não encontrado. Obtendo novo token...");
                CacheManagerHP.GetAccessTokenAsync().Wait();
                accessToken = CacheManagerHP.GetSavedAccessToken();

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
                    Console.WriteLine($"[INFO]: Nenhuma imagem encontrada para o SKU na API: {formatedSku}");
                    return new List<MetaData>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR]: Erro ao processar imagens para o SKU {formatedSku}: {ex.Message}");
                return new List<MetaData>();
            }
        }

        private static string ConvertToKg(string weight, string sku)
        {
            string weightValue = weight.Replace(" ", "");

            if(weightValue.Contains("lb"))
            {
                weightValue = weightValue.Replace("lb", "");

                if(weightValue.Contains("-"))
                {
                    weightValue = weightValue.Split("-")[0];
                }

                weightValue = (double.Parse(weightValue) * 0.45359237).ToString("F2");
                Console.WriteLine($"[INFO]: Peso convertido para kg: {weightValue}");
            }
            else if(weightValue.Contains("kg"))
            {
                weightValue = weightValue.Replace("kg", "");
                Console.WriteLine($"[INFO]: Peso já está em kg, tratamento realizado: {weightValue}");
            }
            else
            {
                Console.WriteLine($"[ERROR]: Peso não encontrado para o SKU {sku}");
                return string.Empty;
            }

            return weightValue;
        }

        private static string ExtractDimension(string dimensions, int position)
        {
            var dimension = dimensions.ToLower();
            var deafaultDimensions = new List<string>
            {
                "176.2",
                "69.8",
                "102.0"
            };
            
            try
            {
                //MM -> CM
                if(dimension.Contains("mm"))
                {
                    Console.WriteLine($"[INFO]: Dimensão em mm: {dimension.Split("x")[position]}");
                    double dimensionCm = double.Parse(dimension.Split("x")[position].Replace("mm", "")) / 10;
                    Console.WriteLine($"[INFO]: Dimensão convertida para cm: {dimensionCm}");

                    return dimensionCm.ToString("F1");
                }
                //CM -> CM
                else if(dimension.Contains("cm"))
                {
                    Console.WriteLine($"[INFO]: Dimensão em cm: {dimension.Split("x")[position]}");
                    double dimensionCm = double.Parse(dimension.Split("x")[position].Replace("cm", ""));

                    return dimensionCm.ToString("F1");
                }
                //IN -> CM
                else if(dimension.Contains("in"))
                {
                    Console.WriteLine($"[INFO]: Dimensão em in: {dimension.Split("x")[position]}");
                    double dimensionCm = double.Parse(dimension.Split("x")[position].Replace("in", "")) * 2.54;
                    Console.WriteLine($"[INFO]: Dimensão convertida para cm: {dimensionCm}");

                    return dimensionCm.ToString("F1");
                }
                //DEFAULT
                else
                {
                    Console.WriteLine($"[ERROR]: Dimensão não encontrada: {dimension.Split("x")[position]}");
                    Console.WriteLine("[WARNING]: Aplicando dimensão default");

                    return deafaultDimensions[position];
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR]: Erro ao processar dimensão: {ex.Message}");
                Console.WriteLine("[WARNING]: Aplicando dimensão default");

                return deafaultDimensions[position];
            }
        }
    }