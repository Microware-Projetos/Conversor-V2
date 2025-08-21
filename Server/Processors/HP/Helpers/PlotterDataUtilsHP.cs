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
        private static readonly string URL_PRODUCT = "https://partner.hp.com/o/headless-product-catalog/v1.0/product";
        private static readonly string HP_COOKIE = "JSESSIONID=34127AEB1363FDAC3038ECEE125EA888.tomcatpfp1-g7t16160s; COOKIE_SUPPORT=true; GUEST_LANGUAGE_ID=en_US";
        private static readonly string PLOTTER_CACHE_FILE = "/app/eCommerce/Server/Cache/HP/plotterCache.json";
        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task<List<WooAttribute>> ProcessAttributes(string sku)
        {
            var formatedSku = sku.Split('#')[0];
            var attributes = new List<WooAttribute>();

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
                    if(File.ReadAllText(PLOTTER_CACHE_FILE).Contains(sku))
                    {
                        System.Console.WriteLine($"[INFO]: Atributos processados no cache para o SKU: {formatedSku}");
                        attributes = TakeAttributes(sku);

                        if(attributes != null && attributes.Any())
                            return attributes;
                        else
                            Console.WriteLine($"[INFO]: Atributos não encontrados para o SKU: {formatedSku}");
                    }
                    else
                    {
                        Console.WriteLine($"[INFO]: Dados do plotter não encontrados no cache para o SKU: {formatedSku}");
                    }
                }
                else
                {
                    Console.WriteLine($"[INFO]: Dados do plotter não encontrados no cache para o SKU: {formatedSku}");
                }

                Console.WriteLine("[INFO]: Processando atributos via API...");
                attributes = await ProcessAttributesApi(sku);
                
                
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
            var attributes = new List<WooAttribute>();
            
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
                attributes = TakeAttributes(sku);

                return attributes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR]: Erro ao processar atributos por API para o SKU {formatedSku}: {ex.Message}");
                return new List<WooAttribute>();
            }
        }

        private static List<WooAttribute> TakeAttributes(string sku)
        {
            var attributes = new List<WooAttribute>();
            
            var cachedJson = File.ReadAllText(PLOTTER_CACHE_FILE);
            var plotterJsonObj  = JObject.Parse(cachedJson);

            if (plotterJsonObj[sku] != null)
            {
                Console.WriteLine($"[INFO]: Dados do plotter encontrados no cache para o SKU: {sku}");

                attributes.Add(new WooAttribute
                {
                    id = 16,
                    options = plotterJsonObj[sku]["whatsinbox"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 21,
                    options = plotterJsonObj[sku]["powersupply"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 22,
                    options = plotterJsonObj[sku]["wrntyfeatures"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 25,
                    options = plotterJsonObj[sku]["memstd"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 39,
                    options = plotterJsonObj[sku]["hdcapprntr"]?.ToString() ?? string.Empty,
                    visible = true
                });
                if(plotterJsonObj[sku]["facet_feat"] != null)
                {   
                    attributes.Add(new WooAttribute
                    {
                        id = 42,
                        options = "Touchscreen",
                        visible = true
                    });
                }
                else
                {
                    attributes.Add(new WooAttribute
                    {
                        id = 42,
                        options = "Não",
                        visible = true
                    });
                }
                    
                attributes.Add(new WooAttribute
                {
                    id = 44,
                    options = plotterJsonObj[sku]["facet_connect"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 82,
                    options = plotterJsonObj[sku]["inktypes"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 827,
                    options = plotterJsonObj[sku]["prntcartclr"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 828,
                    options = plotterJsonObj[sku]["prntheads"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 829,
                    options = plotterJsonObj[sku]["connectstd"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 830,
                    options = plotterJsonObj[sku]["tempopcent"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 831,
                    options = plotterJsonObj[sku]["tempstrgcent"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 832,
                    options = plotterJsonObj[sku]["prntspdgenmet"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 833,
                    options = plotterJsonObj[sku]["prnttech"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 834,
                    options = plotterJsonObj[sku]["aiofunctions"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 835,
                    options = plotterJsonObj[sku]["prntresclrbest"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 836,
                    options = plotterJsonObj[sku]["prntlangstd"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 837,
                    options = plotterJsonObj[sku]["prntmargcutshtmet"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 838,
                    options = plotterJsonObj[sku]["lineaccuracy"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 839,
                    options = plotterJsonObj[sku]["colorstability"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 840,
                    options = plotterJsonObj[sku]["mediasizestdmet"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 841,
                    options = plotterJsonObj[sku]["mediasizecustmet"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 842,
                    options = plotterJsonObj[sku]["mediatype"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 843,
                    options = plotterJsonObj[sku]["mediarollextdiammet"]?.ToString() ?? string.Empty,
                    visible = true
                });
                attributes.Add(new WooAttribute
                {
                    id = 844,
                    options = plotterJsonObj[sku]["mediathickbypath"]?.ToString() ?? string.Empty,
                    visible = true
                });
            }
            else
            {
                Console.WriteLine($"[INFO]: Dados do plotter não encontrados no cache para o SKU: {sku}");
                Console.WriteLine("[WARNING]: Retornando atributos default");

                return attributes;
            }

            return attributes;
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

        public static async Task<string> ProcessDescription(string sku)
        {
            Console.WriteLine($"[INFO]: Processando descrição para o SKU: {sku}");
            var formatedSku = sku.Split('#')[0];
            var description = string.Empty;

            if (!File.Exists(PLOTTER_CACHE_FILE))
            {
                Console.WriteLine($"[INFO]: Arquivo de cache não encontrado!");
                return string.Empty;
            }

            var cacheData = File.ReadAllText(PLOTTER_CACHE_FILE);
            var jsonCacheObj = JObject.Parse(cacheData);

            // Debug: Verificar a estrutura do cache
            Console.WriteLine($"[DEBUG]: Verificando estrutura do cache para SKU: {sku}");
            
            if (jsonCacheObj[sku] != null)
            {
                Console.WriteLine($"[DEBUG]: SKU encontrado no cache");
                
                if (jsonCacheObj[sku]["description"] != null)
                {
                    
                    var descriptionNode = jsonCacheObj[sku]["description"];
                    if (descriptionNode != null)
                    {
                        Console.WriteLine($"[DEBUG]: Descrição encontrada no cache. Tipo: {descriptionNode.Type}");
                        Console.WriteLine($"[DEBUG]: Valor da descrição: {descriptionNode}");
                        
                        // Tentar diferentes tipos de acesso
                        if (descriptionNode.Type == JTokenType.Array)
                        {
                            var descriptionArray = descriptionNode as JArray;
                            if (descriptionArray != null && descriptionArray.Any())
                            {
                                description = descriptionArray.ToString();
                                Console.WriteLine($"[INFO]: Descrição encontrada no cache (Array): {description}");
                                return description;
                            }
                        }
                        else if (descriptionNode.Type == JTokenType.String)
                        {
                            description = descriptionNode.ToString();
                            Console.WriteLine($"[INFO]: Descrição encontrada no cache (String): {description}");
                            return description;
                        }
                        else
                        {
                            description = descriptionNode.ToString();
                            Console.WriteLine($"[INFO]: Descrição encontrada no cache (Outro tipo): {description}");
                            return description;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[DEBUG]: Campo 'description' não encontrado em 'data'");
                    }
                }
                else
                {
                    Console.WriteLine($"[DEBUG]: Seção 'data' não encontrada para o SKU");
                }
            }
            else
            {
                Console.WriteLine($"[DEBUG]: SKU não encontrado no cache");
            }

            Console.WriteLine($"[INFO]: Descrição não encontrada no cache para o SKU: {sku}");
            description = await ProcessDescriptionAPI(sku);
            return description;
        }

        private static async Task<string> ProcessDescriptionAPI(string sku)
        {
            Console.WriteLine($"[INFO]: Processando descrição para o SKU: {sku} via API");

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
                    return string.Empty;
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
            try
            {
                var jsonBody = JsonConvert.SerializeObject(body);
                var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(URL_PRODUCT, content);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[ERROR]: Erro ao obter dados. Status: {response.StatusCode}");
                    return string.Empty;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonObj = JObject.Parse(responseContent);
                var description = string.Empty;

                try
                {

                        var tags = new [] 
                        { 
                            "proddes_overview_medium",
                            "ksp_01_headline_short",
                            "ksp_01_suppt_01_medium",
                            "ksp_01_suppt_02_medium",
                            "ksp_01_suppt_03_medium",
                            "ksp_01_suppt_04_medium",
                            "ksp_02_headline_short",
                            "ksp_02_suppt_01_medium",
                            "ksp_02_suppt_02_medium",
                            "ksp_02_suppt_03_medium",
                            "ksp_02_suppt_04_medium",
                            "ksp_03_headline_short",
                            "ksp_03_suppt_01_medium",
                            "ksp_03_suppt_02_medium",
                            "ksp_03_suppt_03_medium",
                            "ksp_03_suppt_04_medium"
                        };

                        // Criar um dicionário para armazenar os valores das tags encontradas
                        var tagsEncontradasDict = new Dictionary<string, string>();
                        
                        // Primeiro, coletar todas as tags e seus valores
                        foreach (var detail in jsonObj["product"]?
                            .SelectMany(p => p["chunks"])
                            .SelectMany(c => c["details"] ?? Enumerable.Empty<JToken>()))
                        {
                            var name = detail["tag"]?.ToString();
                            var value = detail["value"]?.ToString();

                            // Verificar se a tag atual é uma das que estamos procurando
                            if (!string.IsNullOrEmpty(name) && tags.Contains(name))
                            {
                                if (!string.IsNullOrEmpty(value))
                                {
                                    tagsEncontradasDict[name] = value;
                                    System.Console.WriteLine($"[INFO]: Tag encontrada: {name} = {value}");
                                }
                                else
                                {
                                    Console.WriteLine($"[WARNING]: Tag '{name}' encontrada mas sem valor");
                                }
                            }
                        }
                        
                        // Agora construir a descrição na ordem específica das tags
                        foreach (var tag in tags)
                        {
                            if (tagsEncontradasDict.TryGetValue(tag, out var value))
                            {
                                description += $"{value} ";
                                Console.WriteLine($"[INFO]: Adicionando tag na ordem: {tag}");
                            }
                            else
                            {
                                Console.WriteLine($"[INFO]: Tag não encontrada na API: {tag}");
                            }
                        }
                        
                        // Log das tags encontradas
                        var tagsEncontradas = tagsEncontradasDict.Count;
                        Console.WriteLine($"[INFO]: Total de tags encontradas: {tagsEncontradas} de {tags.Length}");
                        
                        // Se nenhuma tag foi encontrada, log de aviso
                        if (tagsEncontradas == 0)
                        {
                            Console.WriteLine($"[WARNING]: Nenhuma das tags especificadas foi encontrada para o SKU: {sku}");
                        }   
                    
                    // Log da descrição final construída
                    if (!string.IsNullOrEmpty(description))
                    {
                        Console.WriteLine($"[INFO]: Descrição construída com sucesso. Tamanho: {description.Length} caracteres");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARNING]: Atributos não encontrados para o SKU: {formatedSku}. Erro: {ex.Message}");

                    // Ler o cache atual como JObject
                    var cachedJson = File.ReadAllText(PLOTTER_CACHE_FILE);
                    var jsonCacheObj = JObject.Parse(cachedJson);
                    
                    // Definir a descrição como null no cache
                    if (jsonCacheObj[sku] != null)
                    {
                        // Garantir que a estrutura existe
                        if (jsonCacheObj[sku] == null)
                        {
                            jsonCacheObj[sku] = new JObject();
                        }
                        
                        jsonCacheObj[sku]["description"] = null;
                        string newJson = jsonCacheObj.ToString(Formatting.Indented);
                        File.WriteAllText(PLOTTER_CACHE_FILE, newJson);
                        Console.WriteLine($"[INFO]: Dados do produto salvos como NULL em {PLOTTER_CACHE_FILE}");
                    }

                    return string.Empty;
                }
                
                // Salvar a descrição construída no cache
                if (!string.IsNullOrEmpty(description))
                {
                    try
                    {
                        // Ler o cache atual como JObject
                        var cachedJson = File.ReadAllText(PLOTTER_CACHE_FILE);
                        var jsonCacheObj = JObject.Parse(cachedJson);
                        
                        // Atualizar a descrição no cache
                        if (jsonCacheObj[sku] != null)
                        {
                            // Garantir que a estrutura existe
                            if (jsonCacheObj[sku] == null)
                            {
                                jsonCacheObj[sku] = new JObject();
                            }
                            
                            // Salvar como string (não como array)
                            jsonCacheObj[sku]["description"] = description;
                            string json = jsonCacheObj.ToString(Formatting.Indented);
                            File.WriteAllText(PLOTTER_CACHE_FILE, json);
                            Console.WriteLine($"[INFO]: Descrição salva no cache para o SKU: {sku}");
                            Console.WriteLine($"[DEBUG]: Estrutura salva: {jsonCacheObj[sku]["description"]}");
                        }
                        else
                        {
                            Console.WriteLine($"[WARNING]: SKU {sku} não encontrado no cache para salvar descrição");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[WARNING]: Erro ao salvar descrição no cache: {ex.Message}");
                    }
                }
                
                Console.WriteLine($"[INFO]: Descrição construída: {description}");
                // Retornar a descrição construída em vez de string vazia
                return description;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR]: Erro ao processar descrição para o SKU: {sku}. {ex.Message}");
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