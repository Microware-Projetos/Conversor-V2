using eCommerce.Shared.Models;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using eCommerce.Server.Helpers;

namespace eCommerce.Server.Processors.HP;

public static class DataUtilsHP
{
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
                                        
                                        Console.WriteLine($"\nProcessando dimensões da API...");
                                        foreach (var dimItem in productDims)
                                        {
                                            if (dimItem != null && dimItem.Type == JTokenType.Object)
                                            {
                                                var dimObj = dimItem as JObject;
                                                if (dimObj != null)
                                                {
                                                    var name = dimObj["name"]?.ToString();
                                                    var value = dimObj["value"]?.ToString();
                                                    
                                                    Console.WriteLine($"Campo encontrado: '{name}' = '{value}'");
                                                    
                                                    // Verifica se o campo contém "dimension" ou "dimensão" (case insensitive)
                                                    var isDimensionField = dimensionFields.Contains(name) || 
                                                                          (name != null && (name.ToLower().Contains("dimension") || 
                                                                                           name.ToLower().Contains("dimensão")));
                                                    
                                                    if (isDimensionField && !string.IsNullOrWhiteSpace(value))
                                                    {
                                                        Console.WriteLine($"Campo de dimensão encontrado: '{name}'");
                                                        var originalValue = value.ToLower();
                                                        
                                                        // Verifica se tem unidades de medida
                                                        if (!originalValue.Contains("cm") && !originalValue.Contains("mm") && !originalValue.Contains("in"))
                                                        {
                                                            Console.WriteLine($"Campo não contém unidades de medida: {originalValue}");
                                                            continue;
                                                        }
                                                        
                                                        Console.WriteLine($"Processando valor: {originalValue}");
                                                        
                                                        // Tenta processar como três dimensões primeiro
                                                        var result = ProcessThreeDimensions(originalValue);
                                                        if (result != null)
                                                        {
                                                            Console.WriteLine($"Dimensões processadas com sucesso: L={result.length}, W={result.width}, H={result.height}");
                                                            Console.WriteLine($"Tipo do objeto result: {result.GetType().Name}");
                                                            Console.WriteLine($"Verificando propriedades: length='{result.length}', width='{result.width}', height='{result.height}'");
                                                            return result;
                                                        }
                                                        
                                                        // Se não conseguiu processar como três dimensões, tenta como dimensão única
                                                        result = ProcessSingleDimension(originalValue);
                                                        if (result != null)
                                                        {
                                                            Console.WriteLine($"Dimensão única processada: W={result.width}");
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
        Console.WriteLine($"Extraindo dimensões de: '{value}'");
        
        // Tenta encontrar padrões de dimensões (três números separados por x)
        var pattern = @"(\d+\.?\d*)\s*x\s*(\d+\.?\d*)\s*x\s*(\d+\.?\d*)";
        var matches = Regex.Matches(value, pattern);
        
        if (matches.Count > 0)
        {
            // Pega o primeiro conjunto de dimensões encontrado
            var match = matches[0];
            var result = new[] { match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value };
            Console.WriteLine($"Dimensões extraídas com regex: {string.Join(" x ", result)}");
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
        Console.WriteLine($"Limpando valor de dimensão: '{value}'");
        
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
        
        Console.WriteLine($"Valor limpo: '{value}'");
        return value;
    }
    
    private static string ConvertToCm(string value, string originalValue)
    {
        try
        {
            Console.WriteLine($"Convertendo valor: '{value}' de '{originalValue}'");
            
            // Se o valor original contém mm, converte para cm
            if (originalValue.Contains("mm"))
            {
                var convertedValue = (float.Parse(value) / 10f).ToString();
                Console.WriteLine($"Convertido de mm para cm: {value}mm -> {convertedValue}cm");
                return convertedValue;
            }
            // Se o valor original contém in (polegadas), converte para cm
            else if (originalValue.Contains("in"))
            {
                var convertedValue = (float.Parse(value) * 2.54f).ToString();
                Console.WriteLine($"Convertido de in para cm: {value}in -> {convertedValue}cm");
                return convertedValue;
            }
            
            Console.WriteLine($"Valor mantido como está: {value}");
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
    
    public static List<MetaData> ProcessarFotos(string sku, string model, List<object> images, Dictionary<string, string> normalizedFamily, object productAttributesAPI, string sheetName)
    {
        var metaData = new List<MetaData>();
        
        try
        {
            Console.WriteLine($"\n=== Processando fotos para SKU: {sku} ===");
            
            // 1. PRIMEIRA TENTATIVA: ProcessarFotosAlternativo (busca por família/categoria específica)
            Console.WriteLine($"1. Tentando ProcessarFotosAlternativo para {sku}...");
            var fotosAlternativo = ProcessarFotosAlternativo(sku, model, images, normalizedFamily, sheetName, false); // false = não usar default
            if (fotosAlternativo.Count > 0)
            {
                Console.WriteLine($"{sku} - Foto encontrada por família/categoria");
                return fotosAlternativo;
            }
            
            // 2. SEGUNDA TENTATIVA: Cache de Produtos
            Console.WriteLine($"2. Tentando cache de produtos para {sku}...");
            var fotosCache = VerificarFotoNoCache(sku);
            if (fotosCache.Count > 0)
            {
                Console.WriteLine($"{sku} - Usando foto do cache de produtos");
                return fotosCache;
            }
            
            // 3. TERCEIRA TENTATIVA: API HP Atual
            Console.WriteLine($"3. Tentando API HP atual para {sku}...");
            if (productAttributesAPI != null)
            {
                var jsonData = JObject.FromObject(productAttributesAPI);
                var dataNode = jsonData["data"];
                
                if (dataNode != null && dataNode.Type == JTokenType.Object)
                {
                    var devicesNode = dataNode["devices"];
                    if (devicesNode != null && devicesNode.Type == JTokenType.Array)
                    {
                        var devices = devicesNode as JArray;
                        if (devices != null && devices.Count > 0)
                        {
                            var device = devices[0];
                            var productSpecs = device["productSpecs"];
                            
                            if (productSpecs != null && productSpecs.Type == JTokenType.Object)
                            {
                                var specsData = productSpecs["data"];
                                if (specsData != null && specsData.Type == JTokenType.Object)
                                {
                                    var imageUri = specsData["imageUri"]?.ToString();
                                    if (!string.IsNullOrEmpty(imageUri))
                                    {
                                        // Verifica se a URL é completa (começa com http ou https)
                                        if (imageUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                                            imageUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                        {
                                            Console.WriteLine($"{sku} - Foto encontrada na API HP atual");
                                            metaData.Add(new MetaData
                                            {
                                                key = "_external_image_url",
                                                value = imageUri
                                            });
                                            return metaData;
                                        }
                                        else
                                        {
                                            Console.WriteLine($"{sku} - URL incompleta na API HP, ignorando: {imageUri}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            // 4. QUARTA TENTATIVA: Fotos Default (última tentativa)
            Console.WriteLine($"4. Não encontrou em nenhum lugar, usando fotos default para {sku}...");
            var fotosDefault = ProcessarFotosAlternativo(sku, model, images, normalizedFamily, sheetName, true); // true = usar default
            if (fotosDefault.Count > 0)
            {
                Console.WriteLine($"{sku} - Usando fotos default");
            }
            else
            {
                Console.WriteLine($"{sku} - Produto sem foto");
            }
            return fotosDefault;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao processar imagem do produto {sku}: {ex.Message}");
            // Se houver erro, tenta usar o método alternativo de busca de imagens
            return ProcessarFotosAlternativo(sku, model, images, normalizedFamily, sheetName, true);
        }
    }
    
    private static List<MetaData> VerificarFotoNoCache(string sku)
    {
        var metaData = new List<MetaData>();
        
        try
        {
            // Limpa o SKU removendo sufixos como #ABA
            var cleanSku = sku.Split('#')[0].Trim();
            Console.WriteLine($"Verificando cache para SKU limpo: '{cleanSku}' (original: '{sku}')");
            
            var cacheFile = "Cache/HP/product_cache.json";
            if (!File.Exists(cacheFile))
            {
                Console.WriteLine($"Arquivo de cache não encontrado: {cacheFile}");
                return metaData;
            }
            
            var cacheData = CacheManager.LoadCache(cacheFile);
            if (cacheData.ContainsKey(cleanSku))
            {
                Console.WriteLine($"Produto encontrado no cache: {cleanSku}");
                var productData = cacheData[cleanSku];
                var productJson = JObject.FromObject(productData);
                
                // O CacheManager.LoadCache retorna apenas o "data" da estrutura
                // Então productJson já é o "data" do cache
                var dataNode = productJson["data"];
                if (dataNode != null && dataNode.Type == JTokenType.Object)
                {
                    var devicesNode = dataNode["devices"];
                    if (devicesNode != null && devicesNode.Type == JTokenType.Array)
                    {
                        var devices = devicesNode as JArray;
                        if (devices != null && devices.Count > 0)
                        {
                            var device = devices[0];
                            var productSpecs = device["productSpecs"];
                            
                            if (productSpecs != null && productSpecs.Type == JTokenType.Object)
                            {
                                var specsData = productSpecs["data"];
                                if (specsData != null && specsData.Type == JTokenType.Object)
                                {
                                    var imageUri = specsData["imageUri"]?.ToString();
                                    if (!string.IsNullOrEmpty(imageUri))
                                    {
                                        // Verifica se a URL é completa (começa com http ou https)
                                        if (imageUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                                            imageUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                        {
                                            Console.WriteLine($"Foto encontrada no cache: {imageUri}");
                                            metaData.Add(new MetaData
                                            {
                                                key = "_external_image_url",
                                                value = imageUri
                                            });
                                            return metaData;
                                        }
                                        else
                                        {
                                            Console.WriteLine($"URL incompleta no cache, ignorando: {imageUri}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("imageUri não encontrado ou vazio no cache");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("specsData não encontrado no cache");
                                }
                            }
                            else
                            {
                                Console.WriteLine("productSpecs não encontrado no cache");
                            }
                        }
                        else
                        {
                            Console.WriteLine("devices array vazio no cache");
                        }
                    }
                    else
                    {
                        Console.WriteLine("devices não encontrado no cache");
                    }
                }
                else
                {
                    Console.WriteLine("dataNode não encontrado no cache");
                }
            }
            else
            {
                Console.WriteLine($"Produto não encontrado no cache: {cleanSku}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao verificar foto no cache para {sku}: {ex.Message}");
        }
        
        return metaData;
    }
    
    private static List<MetaData> ProcessarFotosAlternativo(string sku, string model, List<object> images, Dictionary<string, string> normalizedFamily, string sheetName, bool useDefault = true)
    {
        var metaData = new List<MetaData>();
        
        try
        {
            // Define coluna com base na sheet (para logs)
            string colunaInfo = "SKU"; // Valor padrão
            if (sheetName == "SmartChoice")
                colunaInfo = "PN";
            else if (sheetName == "Portfólio Acessorios_Monitores")
                colunaInfo = "SKU";
            else
                colunaInfo = "Model";
            
            var baseUrl = "https://eprodutos-integracao.microware.com.br/api/photos/image/";
            var filteredImages = new List<object>();
            
            // Busca família normalizada
            string normalizeFamily = "";
            foreach (var kvp in normalizedFamily)
            {
                if (model.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    normalizeFamily = kvp.Value;
                    break;
                }
            }
            
            // Carrega categoria do map.json
            var mapData = JObject.Parse(File.ReadAllText("Maps/HP/map.json"));
            var traducaoLinha = mapData["TraducaoLinha"]?.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>();
            var category = traducaoLinha.GetValueOrDefault(sheetName, "Acessorio");
            
            var searchTerm = model ?? "";
            
            // Filtra imagens por família
            foreach (var image in images)
            {
                var imageObj = JObject.FromObject(image);
                var family = imageObj["family"]?.ToString();
                
                if (!string.IsNullOrEmpty(family) && searchTerm.Contains(family, StringComparison.OrdinalIgnoreCase))
                {
                    filteredImages.Add(image);
                }
            }
            
            // Se não encontrar, tenta com a família normalizada
            if (filteredImages.Count == 0)
            {
                foreach (var image in images)
                {
                    var imageObj = JObject.FromObject(image);
                    var manufacturer = imageObj["manufacturer"]?.ToString();
                    var imageCategory = imageObj["category"]?.ToString();
                    var family = imageObj["family"]?.ToString();
                    
                    if (manufacturer == "HP" && imageCategory == category && family == normalizeFamily)
                    {
                        filteredImages.Add(image);
                    }
                }
            }
            
            // Se ainda estiver vazio E useDefault for true, tenta com a família Default
            if (filteredImages.Count == 0 && useDefault)
            {
                string defaultCategory;
                if (sheetName == "Portfólio Acessorios_Monitores")
                {
                    // Aqui você precisaria ter acesso ao PL Description do produto
                    defaultCategory = "Monitor"; // Simplificado
                }
                else
                {
                    var defaultPhotos = mapData["DefaultPhotos"]?.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>();
                    defaultCategory = defaultPhotos.GetValueOrDefault(sheetName, "Acessorio");
                }
                
                foreach (var image in images)
                {
                    var imageObj = JObject.FromObject(image);
                    var manufacturer = imageObj["manufacturer"]?.ToString();
                    var imageCategory = imageObj["category"]?.ToString();
                    var family = imageObj["family"]?.ToString();
                    
                    if (manufacturer == "HP" && imageCategory == defaultCategory && family == "Default")
                    {
                        filteredImages.Add(image);
                        Console.WriteLine($"{sku} - Produto com foto default");
                    }
                }
            }
            
            // Cria a lista de URLs das imagens
            var imageUrls = new List<string>();
            foreach (var image in filteredImages)
            {
                var imageObj = JObject.FromObject(image);
                var id = imageObj["id"]?.ToString();
                var extension = imageObj["extension"]?.ToString();
                
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(extension))
                {
                    var imageUrl = $"{baseUrl}{id}.{extension}";
                    imageUrls.Add(imageUrl);
                }
            }
            
            if (imageUrls.Count == 0)
            {
                Console.WriteLine($"{sku} - Produto sem foto");
                return metaData;
            }
            
            // Primeira imagem é a principal (thumbnail), o resto é galeria
            metaData.Add(new MetaData
            {
                key = "_external_image_url",
                value = imageUrls[0]
            });
            
            if (imageUrls.Count > 1)
            {
                // Para galeria, retorna array de URLs
                var galleryUrls = imageUrls.Skip(1).ToArray();
                Console.WriteLine($"Galeria para {sku}: {galleryUrls.Length} imagens");
                metaData.Add(new MetaData
                {
                    key = "_external_gallery_images",
                    value = galleryUrls
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao processar fotos alternativo para {sku}: {ex.Message}");
        }
        
        return metaData;
    }
} 