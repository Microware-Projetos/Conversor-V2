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
using System.Text.RegularExpressions;
using System.IO;
using WooAttribute = eCommerce.Shared.Models.Attribute;
using eCommerce.Shared.Models;
using eCommerce.Server.Helpers;
using eCommerce.Server.Processors.HP;
using eCommerce.Server.Processors.HP.Helpers;

namespace eCommerce.Server.Processors.HP.Helpers;

public static class ProductDataUtilsHP
{
    private static readonly string CHACHE_DIR = "/app/eCommerce/Server/Cache/HP";
    private static readonly string URL_PRODUCT = "https://partner.hp.com/o/headless-product-catalog/v1.0/product";
    private static readonly string HP_COOKIE = "JSESSIONID=34127AEB1363FDAC3038ECEE125EA888.tomcatpfp1-g7t16160s; COOKIE_SUPPORT=true; GUEST_LANGUAGE_ID=en_US";
    private static readonly string PRODUCT_CACHE_FILE = "/app/eCommerce/Server/Cache/HP/product_cache.json";
    private static readonly HttpClient _httpClient = new HttpClient()
    {
        Timeout = TimeSpan.FromMinutes(2)
    };

    public static string ProcessWeight(string weight, object product_attributes)
    {
        try
        {
            if (product_attributes != null)
            {
                // Converte para JObject para navegar na estrutura JSON
                var jsonData = JObject.FromObject(product_attributes);
                
                // Verifica se a estrutura data.products existe e é um objeto
                var dataNode = jsonData["data"];
                if (dataNode != null && dataNode.Type == JTokenType.Object)
                {
                    var productsNode = dataNode["products"];
                    if (productsNode != null && productsNode.Type == JTokenType.Object)
                    {
                        var products = productsNode as JObject;
                        if (products != null)
                        {
                            // Pega o primeiro SKU da lista de produtos
                            var firstSku = products.Properties().FirstOrDefault()?.Name;
                            if (!string.IsNullOrEmpty(firstSku))
                            {
                                var productWeightNode = products[firstSku];
                                if (productWeightNode != null && productWeightNode.Type == JTokenType.Array)
                                {
                                    var productWeight = productWeightNode as JArray;
                                    
                                    if (productWeight != null)
                                    {
                                        // Lista de possíveis nomes de campos que podem conter peso
                                        var weightFields = new[] { "Peso com embagem", "Package weight", "Peso", "Weight" };
                                        
                                        foreach (var weightItem in productWeight)
                                        {
                                            if (weightItem != null && weightItem.Type == JTokenType.Object)
                                            {
                                                var weightObj = weightItem as JObject;
                                                if (weightObj != null)
                                                {
                                                    var name = weightObj["name"]?.ToString();
                                                    var value = weightObj["value"]?.ToString();
                                                    
                                                    if (weightFields.Contains(name) && !string.IsNullOrWhiteSpace(value))
                                                    {
                                                        var originalValue = value.ToLower();
                                                        
                                                        // Verifica se tem unidades de medida
                                                        if (!originalValue.Contains("kg") && !originalValue.Contains("lb"))
                                                            continue;
                                                        
                                                        // Processa o peso
                                                        var result = ProcessWeightValue(originalValue);
                                                        if (!string.IsNullOrEmpty(result))
                                                            return result;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao processar peso da API para produto: {ex.Message}");
        }
        
        // Fallback para o processamento original
        return ProcessWeightOriginal(weight);
    }
    
    private static string ProcessWeightValue(string originalValue)
    {
        try
        {
            // Se tem múltiplos valores, prioriza kg sobre lb
            if (originalValue.Contains("kg") && originalValue.Contains("lb"))
            {
                // Extrai o valor em kg primeiro
                var kgMatch = Regex.Match(originalValue, @"(\d+[.,]?\d*)\s*kg");
                if (kgMatch.Success)
                {
                    var kgValue = kgMatch.Groups[1].Value.Replace(",", ".");
                    return kgValue;
                }
                
                // Se não encontrou kg, tenta lb
                var lbMatch = Regex.Match(originalValue, @"(\d+[.,]?\d*)\s*lb");
                if (lbMatch.Success)
                {
                    var lbValue = lbMatch.Groups[1].Value.Replace(",", ".");
                    return ConvertToKg(lbValue, originalValue);
                }
            }
            else
            {
                // Processamento normal para um valor
                var cleanValue = CleanWeightValue(originalValue);
                
                // Tenta extrair o primeiro número encontrado
                var numbers = Regex.Matches(cleanValue, @"[-+]?\d*\.\d+|\d+");
                if (numbers.Count > 0)
                {
                    var numberValue = numbers[0].Value;
                    // Converte para kg se necessário
                    var result = ConvertToKg(numberValue, originalValue);
                    if (!string.IsNullOrEmpty(result))
                        return result;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao processar valor de peso: {ex.Message}");
        }
        
        return "";
    }
    
    private static string CleanWeightValue(string value)
    {
        // Remove textos comuns que podem aparecer
        var removeTexts = new[] { 
            "with stand", "without stand", "starting at", "a partir de", 
            "approximately", "approx", "aprox", "min", "max", "minimum", 
            "maximum", "from", "de", "to", "até", "&lt;br /&gt;", "&gt;", "&lt;",
            "configurado com", "configurado", "uma unidade", "unidade de disco", 
            "disco rígido", "unidade ótica", "ótica", "o peso pode variar", 
            "de acordo com a configuração", "peso pode variar", "acordo com",
            "configuração", "pode variar"
        };
        
        foreach (var text in removeTexts)
        {
            value = value.Replace(text, "", StringComparison.OrdinalIgnoreCase);
        }
        
        // Remove parênteses e seu conteúdo
        value = Regex.Replace(value, @"\([^)]*\)", "");
        
        // Remove texto após ponto e vírgula
        value = value.Split(';')[0].Trim();
        
        // Remove texto após ponto final (seguido de texto descritivo)
        var sentences = value.Split('.');
        if (sentences.Length > 1)
        {
            // Pega apenas a primeira frase que deve conter o peso
            var firstSentence = sentences[0].Trim();
            if (!string.IsNullOrEmpty(firstSentence) && (firstSentence.Contains("kg") || firstSentence.Contains("lb")))
            {
                value = firstSentence;
            }
        }
        
        // Remove unidades e espaços extras
        value = value.Replace("kg", "").Replace("lb", "").Trim();
        
        // Substitui vírgula por ponto
        value = value.Replace(",", ".");
        
        // Remove espaços extras
        value = string.Join(" ", value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        
        return value;
    }
    
    private static string ConvertToKg(string value, string originalValue)
    {
        try
        {
            // Se o valor original contém lb, converte para kg
            if (originalValue.Contains("lb"))
            {
                return (float.Parse(value) * 0.45359237f).ToString();
            }
            return value;
        }
        catch
        {
            return "";
        }
    }
    
    private static string ProcessWeightOriginal(string weight)
    {
        if (string.IsNullOrWhiteSpace(weight))
        {
            return "0";
        }

        if (weight.Contains("kg") ||  weight.Contains("lb") || weight.Contains("Kg") || weight.Contains("Lb"))
        {
            return weight.Replace("kg", "").Replace("lb", "").Replace("Kg", "").Replace("Lb", "");
        }

        return weight;
    }

    public static Dimensions ProcessDimensions(string dimensions, object dimensions_data = null)
    {
        try
        {
            if (dimensions_data != null)
            {
                // Converte para JObject para navegar na estrutura JSON
                var jsonData = JObject.FromObject(dimensions_data);
                
                // Verifica se a estrutura data.products existe e é um objeto
                var dataNode = jsonData["data"];
                if (dataNode != null && dataNode.Type == JTokenType.Object)
                {
                    var productsNode = dataNode["products"];
                    if (productsNode != null && productsNode.Type == JTokenType.Object)
                    {
                        var products = productsNode as JObject;
                        if (products != null)
                        {
                            // Pega o primeiro SKU da lista de produtos
                            var firstSku = products.Properties().FirstOrDefault()?.Name;
                            if (!string.IsNullOrEmpty(firstSku))
                            {
                                var productDimsNode = products[firstSku];
                                if (productDimsNode != null && productDimsNode.Type == JTokenType.Array)
                                {
                                    var productDims = productDimsNode as JArray;
                                    
                                    if (productDims != null)
                                    {
                                        // Lista de possíveis nomes de campos que podem conter dimensões
                                        var dimensionFields = new[] { 
                                            "Dimensões com embalagem",
                                            "Package dimensions (W x D x H)",
                                            "Dimensões (L x P x A)",
                                            "Dimensions (W x D x H)",
                                            "Dimensões mínimas (L x P x A)",
                                            "Dimensões",
                                            "Dimensions",
                                            "Package Dimensions",
                                            "Product Dimensions"
                                        };
                                        
                                        //Console.WriteLine($"\nProcessando dimensões da API...");
                                        foreach (var dimItem in productDims)
                                        {
                                            if (dimItem != null && dimItem.Type == JTokenType.Object)
                                            {
                                                var dimObj = dimItem as JObject;
                                                if (dimObj != null)
                                                {
                                                    var name = dimObj["name"]?.ToString();
                                                    var value = dimObj["value"]?.ToString();
                                                    
                                                    //Console.WriteLine($"Campo encontrado: '{name}' = '{value}'");
                                                    
                                                    // Verifica se o campo contém "dimension" ou "dimensão" (case insensitive)
                                                    var isDimensionField = dimensionFields.Contains(name) || 
                                                                          (name != null && (name.ToLower().Contains("dimension") || 
                                                                                           name.ToLower().Contains("dimensão")));
                                                    
                                                    if (isDimensionField && !string.IsNullOrWhiteSpace(value))
                                                    {
                                                        //Console.WriteLine($"Campo de dimensão encontrado: '{name}'");
                                                        var originalValue = value.ToLower();
                                                        
                                                        // Verifica se tem unidades de medida
                                                        if (!originalValue.Contains("cm") && !originalValue.Contains("mm") && !originalValue.Contains("in"))
                                                        {
                                                            Console.WriteLine($"Campo não contém unidades de medida: {originalValue}");
                                                            continue;
                                                        }
                                                        
                                                        //Console.WriteLine($"Processando valor: {originalValue}");
                                                        
                                                        // Tenta processar como três dimensões primeiro
                                                        var result = ProcessThreeDimensions(originalValue);
                                                        if (result != null)
                                                        {
                                                            //Console.WriteLine($"Dimensões processadas com sucesso: L={result.length}, W={result.width}, H={result.height}");
                                                            //Console.WriteLine($"Tipo do objeto result: {result.GetType().Name}");
                                                            //Console.WriteLine($"Verificando propriedades: length='{result.length}', width='{result.width}', height='{result.height}'");
                                                            return result;
                                                        }
                                                        
                                                        // Se não conseguiu processar como três dimensões, tenta como dimensão única
                                                        result = ProcessSingleDimension(originalValue);
                                                        if (result != null)
                                                        {
                                                            //Console.WriteLine($"Dimensão única processada: W={result.width}");
                                                            return result;
                                                        }
                                                        
                                                        Console.WriteLine($"Não foi possível processar as dimensões: {originalValue}");
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao processar dimensões da API para produto: {ex.Message}");
        }
        
        // Fallback para o processamento original
        return ProcessDimensionsOriginal(dimensions);
    }
    
    private static Dimensions? ProcessThreeDimensions(string value)
    {
        try
        {
            var originalValue = value; // Preserva o valor original com unidades
            value = CleanDimensionValue(value);
            var dimensions = ExtractDimensions(value);
            
            if (dimensions != null)
            {
                var processedDims = new string[3];
                for (int i = 0; i < dimensions.Length; i++)
                {
                    processedDims[i] = ConvertToCm(dimensions[i], originalValue);
                }
                
                return new Dimensions
                {
                    length = processedDims[0],
                    width = processedDims[1],
                    height = processedDims[2]
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao processar três dimensões: {ex.Message}");
        }
        return null;
    }
    
    private static Dimensions? ProcessSingleDimension(string value)
    {
        try
        {
            var originalValue = value; // Preserva o valor original com unidades
            value = CleanDimensionValue(value);
            var numbers = Regex.Matches(value, @"[-+]?\d*\.\d+|\d+");
            if (numbers.Count > 0)
            {
                var dimValue = ConvertToCm(numbers[0].Value, originalValue);
                return new Dimensions
                {
                    length = "0",
                    width = dimValue,
                    height = "0"
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao processar dimensão única: {ex.Message}");
        }
        return null;
    }
    
    private static string[]? ExtractDimensions(string value)
    {
        //Console.WriteLine($"Extraindo dimensões de: '{value}'");
        
        // Tenta encontrar padrões de dimensões (três números separados por x)
        var pattern = @"(\d+\.?\d*)\s*x\s*(\d+\.?\d*)\s*x\s*(\d+\.?\d*)";
        var matches = Regex.Matches(value, pattern);
        
        if (matches.Count > 0)
        {
            // Pega o primeiro conjunto de dimensões encontrado
            var match = matches[0];
            var result = new[] { match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value };
            //Console.WriteLine($"Dimensões extraídas com regex: {string.Join(" x ", result)}");
            return result;
        }
        
        // Se não encontrou o padrão, tenta extrair números individuais
        var numbers = Regex.Matches(value, @"[-+]?\d*\.\d+|\d+");
        if (numbers.Count >= 3)
        {
            var result = new[] { numbers[0].Value, numbers[1].Value, numbers[2].Value };
            Console.WriteLine($"Dimensões extraídas como números individuais: {string.Join(" x ", result)}");
            return result;
        }
        
        Console.WriteLine("Não foi possível extrair dimensões");
        return null;
    }
    
    private static string CleanDimensionValue(string value)
    {
        //Console.WriteLine($"Limpando valor de dimensão: '{value}'");
        
        // Converte para minúsculo
        value = value.ToLower();
        
        // Remove textos comuns que podem aparecer
        var removeTexts = new[] {
            "system dimensions may fluctuate",
            "as dimensões do sistema podem ser diferentes",
            "due to configuration",
            "devido a configurações",
            "manufacturing variances",
            "variâncias na fabricação",
            "keyboard",
            "mouse",
            "teclado",
            "&lt;br /&gt;",
            "&gt;",
            "&lt;"
        };
        
        foreach (var text in removeTexts)
        {
            value = value.Replace(text, "");
        }
        
        // Remove parênteses e seu conteúdo
        value = Regex.Replace(value, @"\([^)]*\)", "");
        
        // Remove texto após ponto e vírgula
        value = value.Split(';')[0].Trim();
        
        // Substitui vírgula por ponto
        value = value.Replace(",", ".");
        
        // Remove espaços extras
        value = string.Join(" ", value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        
        //Console.WriteLine($"Valor limpo: '{value}'");
        return value;
    }
    
    private static string ConvertToCm(string value, string originalValue)
    {
        try
        {
            //Console.WriteLine($"Convertendo valor: '{value}' de '{originalValue}'");
            
            // Se o valor original contém mm, converte para cm
            if (originalValue.Contains("mm"))
            {
                var convertedValue = (float.Parse(value) / 10f).ToString();
                //Console.WriteLine($"Convertido de mm para cm: {value}mm -> {convertedValue}cm");
                return convertedValue;
            }
            // Se o valor original contém in (polegadas), converte para cm
            else if (originalValue.Contains("in"))
            {
                var convertedValue = (float.Parse(value) * 2.54f).ToString();
                //Console.WriteLine($"Convertido de in para cm: {value}in -> {convertedValue}cm");
                return convertedValue;
            }
            
            //Console.WriteLine($"Valor mantido como está: {value}");
            return value;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao converter valor '{value}': {ex.Message}");
            return "0";
        }
    }
    
    private static Dimensions ProcessDimensionsOriginal(string dimensions)
    {
        if (string.IsNullOrWhiteSpace(dimensions))
        {
            return new Dimensions { length = "0", width = "0", height = "0" };
        }

        // Remove espaços extras e normaliza
        dimensions = dimensions.Trim().ToLower();
        
        // Remove "cm" e espaços extras
        dimensions = dimensions.Replace("cm", "").Replace(" ", "");
        
        // Procura por padrões como "31.39x21.99x1.74" ou "31.39 x 21.99 x 1.74"
        var parts = dimensions.Split(new[] { 'x', 'X' }, StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length >= 3)
        {
            // Pega os primeiros 3 valores (length, width, height)
            var length = parts[0].Trim();
            var width = parts[1].Trim();
            var height = parts[2].Trim();
            
            // Remove caracteres não numéricos (exceto ponto decimal)
            length = new string(length.Where(c => char.IsDigit(c) || c == '.').ToArray());
            width = new string(width.Where(c => char.IsDigit(c) || c == '.').ToArray());
            height = new string(height.Where(c => char.IsDigit(c) || c == '.').ToArray());
            
            return new Dimensions 
            { 
                length = string.IsNullOrEmpty(length) ? "0" : length,
                width = string.IsNullOrEmpty(width) ? "0" : width,
                height = string.IsNullOrEmpty(height) ? "0" : height
            };
        }
        
        // Se não conseguir extrair 3 dimensões, retorna zeros
        return new Dimensions { length = "0", width = "0", height = "0" };
    }
    
    public static async Task<List<MetaData>> ProcessPhotos(string sku, string aba)
    {
        Console.WriteLine($"[INFO]: Processando fotos para o SKU: {sku}");
        var metaData = new List<MetaData>();
        var formatedSku = sku.Split('#')[0];
        
        try
        {
            if(File.Exists(PRODUCT_CACHE_FILE))
            {
                var cacheData = File.ReadAllText(PRODUCT_CACHE_FILE);
                var productData =  JsonConvert.DeserializeObject<Dictionary<string, object>>(cacheData) ?? new Dictionary<string, object>();
                if(productData.ContainsKey(formatedSku))
                {
                    Console.WriteLine($"[INFO]: Dados do produto encontrados no cache para o SKU: {formatedSku}");
                    var jsonCacheObj = JObject.Parse(cacheData);
                    var imageProduct = jsonCacheObj[formatedSku]?["data"]?["images"] as JArray;
                    if(imageProduct != null && imageProduct.Any())
                    {
                        Console.WriteLine($"[INFO]: Imagens encontradas para o SKU: {formatedSku}");
                            
                        metaData.Add(new MetaData
                        {
                            key = "_external_image_url",
                            value = imageProduct.First().ToString()
                        });

                        if (imageProduct.Count > 1)
                        {
                            metaData.Add(new MetaData
                            {
                                key = "_external_gallery_images",
                                value = imageProduct.Skip(1).ToList()
                            });
                        }

                        Console.WriteLine($"[INFO]: Total de imagens processadas: {metaData.Count}");
                        return metaData;
                    }
                    else
                    {
                        Console.WriteLine($"[INFO]: Nenhuma imagem encontrada para o SKU: {formatedSku}");
                        Console.WriteLine("[INFO]: Tentando buscar imagens por API...");
                        
                        metaData = await ProcessImagesAPI(sku, aba);

                        if (metaData == null || !metaData.Any())
                        {
                            Console.WriteLine($"[INFO]: Nenhuma foto encontrada para o SKU na API: {formatedSku}");
                            return new List<MetaData>();
                        }

                        Console.WriteLine($"[INFO]: Fotos processadas com sucesso para o SKU: {formatedSku}");
                        return metaData;
                    }
                }
                else
                {
                    Console.WriteLine($"[INFO]: Dados do produto não encontrados no cache para o SKU: {formatedSku}");
                    return new List<MetaData>();
                }
            }
            else
            {
                Console.WriteLine($"[INFO]: Arquivo de cache não encontrado para o SKU: {formatedSku}");
                return new List<MetaData>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR]: Erro ao processar fotos para o SKU {sku}: {ex.Message}");
            return new List<MetaData>();
        }
    }

    private static async Task<List<MetaData>> ProcessImagesAPI(string sku, string aba)
    {
        Console.WriteLine($"[INFO]: Processando imagens para o SKU: {sku}");
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
                Console.WriteLine($"[INFO]: Aba: {aba}");
                var orientationOrder = new string[] {};
                if(aba == "Notebooks")
                {
                    Console.WriteLine("[INFO]: Usando orientação para Notebook");
                    orientationOrder = new[]
                    {
                        "Right Facing",
                        "Left and Right facing",
                        "Center facing",
                        "Left rear facing",
                        "Left facing"
                    };
                }
                else if(aba == "Desktops")
                {
                    Console.WriteLine("[INFO]: Usando orientação para Desktops");
                    orientationOrder = new[]
                    {
                        "Right",
                        "Center",
                        "Left",
                        "Rear",
                        ""
                    };
                }
                else
                {
                    Console.WriteLine("[INFO]: Usando orientação para outras abas");
                    orientationOrder = new[]
                    {
                        "Right",
                        "Other",
                        "Center",
                        "Other",
                        "Left"
                    };
                }
                

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
                Dictionary<string, object> productData = new Dictionary<string, object>();
                var metaData = new List<MetaData>();
                CacheManagerHP.EnsureCacheDir(CHACHE_DIR);

                if (File.Exists(PRODUCT_CACHE_FILE))
                {
                    var cachedJson = File.ReadAllText(PRODUCT_CACHE_FILE);
                    productData = JsonConvert.DeserializeObject<Dictionary<string, object>>(cachedJson) ?? new Dictionary<string, object>();
                }

                if (!productData.ContainsKey(formatedSku))
                {
                    Console.WriteLine($"[INFO]: Adicionando novo SKU ao cache: {formatedSku}");
                    productData[formatedSku] = new JObject();
                }

                var produto = productData[formatedSku] as JObject ?? new JObject();
                
                // Garante que o campo "data" existe
                if (produto["data"] == null)
                {
                    produto["data"] = new JObject();
                }
                
                produto["data"]["images"] = JToken.FromObject(resultImages);
                productData[formatedSku] = produto;

                File.WriteAllText(PRODUCT_CACHE_FILE, JsonConvert.SerializeObject(productData, Formatting.Indented));

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

    public static async Task<string> ProcessDescription(string sku)
    {
        Console.WriteLine($"[INFO]: Processando descrição para o SKU: {sku}");
        var formatedSku = sku.Split('#')[0];
        var description = string.Empty;

        if (!File.Exists(PRODUCT_CACHE_FILE))
        {
            Console.WriteLine($"[INFO]: Arquivo de cache não encontrado!");
            return string.Empty;
        }

        var cacheData = File.ReadAllText(PRODUCT_CACHE_FILE);
        var jsonCacheObj = JObject.Parse(cacheData);

        // Debug: Verificar a estrutura do cache
        Console.WriteLine($"[DEBUG]: Verificando estrutura do cache para SKU: {formatedSku}");
        
        if (jsonCacheObj[formatedSku] != null)
        {
            Console.WriteLine($"[DEBUG]: SKU encontrado no cache");
            
            if (jsonCacheObj[formatedSku]["data"] != null)
            {
                Console.WriteLine($"[DEBUG]: Seção 'data' encontrada");
                
                var descriptionNode = jsonCacheObj[formatedSku]["data"]["description"];
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
                        "proddes_overview_extended",
                        "ksp_01_headline_medium",
                        "ksp_01_suppt_01_long",
                        "ksp_02_headline_medium",
                        "ksp_02_suppt_01_long",
                        "ksp_03_headline_medium",
                        "ksp_03_suppt_01_long",
                        "ksp_04_headline_medium",
                        "ksp_04_suppt_01_long"
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
                        Console.WriteLine($"[WARNING]: Nenhuma das tags especificadas foi encontrada para o SKU: {formatedSku}");
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
                var cachedJson = File.ReadAllText(PRODUCT_CACHE_FILE);
                var jsonCacheObj = JObject.Parse(cachedJson);
                
                // Definir a descrição como null no cache
                if (jsonCacheObj[formatedSku] != null)
                {
                    // Garantir que a estrutura existe
                    if (jsonCacheObj[formatedSku]["data"] == null)
                    {
                        jsonCacheObj[formatedSku]["data"] = new JObject();
                    }
                    
                    jsonCacheObj[formatedSku]["data"]["description"] = null;
                    string newJson = jsonCacheObj.ToString(Formatting.Indented);
                    File.WriteAllText(PRODUCT_CACHE_FILE, newJson);
                    Console.WriteLine($"[INFO]: Dados do produto salvos como NULL em {PRODUCT_CACHE_FILE}");
                }

                return string.Empty;
            }
            
            // Salvar a descrição construída no cache
            if (!string.IsNullOrEmpty(description))
            {
                try
                {
                    // Ler o cache atual como JObject
                    var cachedJson = File.ReadAllText(PRODUCT_CACHE_FILE);
                    var jsonCacheObj = JObject.Parse(cachedJson);
                    
                    // Atualizar a descrição no cache
                    if (jsonCacheObj[formatedSku] != null)
                    {
                        // Garantir que a estrutura existe
                        if (jsonCacheObj[formatedSku]["data"] == null)
                        {
                            jsonCacheObj[formatedSku]["data"] = new JObject();
                        }
                        
                        // Salvar como string (não como array)
                        jsonCacheObj[formatedSku]["data"]["description"] = description;
                        string json = jsonCacheObj.ToString(Formatting.Indented);
                        File.WriteAllText(PRODUCT_CACHE_FILE, json);
                        Console.WriteLine($"[INFO]: Descrição salva no cache para o SKU: {formatedSku}");
                        Console.WriteLine($"[DEBUG]: Estrutura salva: {jsonCacheObj[formatedSku]["data"]["description"]}");
                    }
                    else
                    {
                        Console.WriteLine($"[WARNING]: SKU {formatedSku} não encontrado no cache para salvar descrição");
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
} 