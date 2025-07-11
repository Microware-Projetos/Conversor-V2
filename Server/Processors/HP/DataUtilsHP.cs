using eCommerce.Shared.Models;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

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
                                                        
                                                        // Limpa o valor
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
    
    private static string CleanWeightValue(string value)
    {
        // Remove textos comuns que podem aparecer
        var removeTexts = new[] { 
            "with stand", "without stand", "starting at", "a partir de", 
            "approximately", "approx", "aprox", "min", "max", "minimum", 
            "maximum", "from", "de", "to", "até", "&lt;br /&gt;", "&gt;", "&lt;" 
        };
        
        foreach (var text in removeTexts)
        {
            value = value.Replace(text, "");
        }
        
        // Remove parênteses e seu conteúdo
        value = Regex.Replace(value, @"\([^)]*\)", "");
        
        // Remove texto após ponto e vírgula
        value = value.Split(';')[0].Trim();
        
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
                                            "Dimensões"
                                        };
                                        
                                        foreach (var dimItem in productDims)
                                        {
                                            if (dimItem != null && dimItem.Type == JTokenType.Object)
                                            {
                                                var dimObj = dimItem as JObject;
                                                if (dimObj != null)
                                                {
                                                    var name = dimObj["name"]?.ToString();
                                                    var value = dimObj["value"]?.ToString();
                                                    
                                                    if (dimensionFields.Contains(name) && !string.IsNullOrWhiteSpace(value))
                                                    {
                                                        var originalValue = value.ToLower();
                                                        
                                                        // Verifica se tem unidades de medida
                                                        if (!originalValue.Contains("cm") && !originalValue.Contains("mm") && !originalValue.Contains("in"))
                                                            continue;
                                                        
                                                        // Tenta processar como três dimensões primeiro
                                                        var result = ProcessThreeDimensions(originalValue);
                                                        if (result != null)
                                                            return result;
                                                        
                                                        // Se não conseguiu processar como três dimensões, tenta como dimensão única
                                                        result = ProcessSingleDimension(originalValue);
                                                        if (result != null)
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
            Console.WriteLine($"Erro ao processar dimensões da API para produto: {ex.Message}");
        }
        
        // Fallback para o processamento original
        return ProcessDimensionsOriginal(dimensions);
    }
    
    private static Dimensions ProcessThreeDimensions(string value)
    {
        try
        {
            value = CleanDimensionValue(value);
            var dimensions = ExtractDimensions(value);
            
            if (dimensions != null)
            {
                var processedDims = new string[3];
                for (int i = 0; i < dimensions.Length; i++)
                {
                    processedDims[i] = ConvertToCm(dimensions[i], value);
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
    
    private static Dimensions ProcessSingleDimension(string value)
    {
        try
        {
            value = CleanDimensionValue(value);
            var numbers = Regex.Matches(value, @"[-+]?\d*\.\d+|\d+");
            if (numbers.Count > 0)
            {
                var dimValue = ConvertToCm(numbers[0].Value, value);
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
    
    private static string[] ExtractDimensions(string value)
    {
        // Tenta encontrar padrões de dimensões (três números separados por x)
        var pattern = @"(\d+\.?\d*)\s*x\s*(\d+\.?\d*)\s*x\s*(\d+\.?\d*)";
        var matches = Regex.Matches(value, pattern);
        
        if (matches.Count > 0)
        {
            // Pega o primeiro conjunto de dimensões encontrado
            var match = matches[0];
            return new[] { match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value };
        }
        
        // Se não encontrou o padrão, tenta extrair números individuais
        var numbers = Regex.Matches(value, @"[-+]?\d*\.\d+|\d+");
        if (numbers.Count >= 3)
        {
            return new[] { numbers[0].Value, numbers[1].Value, numbers[2].Value };
        }
        
        return null;
    }
    
    private static string CleanDimensionValue(string value)
    {
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
        
        // Remove unidades e espaços extras
        value = value.Replace("cm", "").Replace("mm", "").Replace("in", "").Trim();
        
        // Substitui vírgula por ponto
        value = value.Replace(",", ".");
        
        // Remove espaços extras
        value = string.Join(" ", value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        
        return value;
    }
    
    private static string ConvertToCm(string value, string originalValue)
    {
        try
        {
            // Se o valor original contém mm, converte para cm
            if (originalValue.Contains("mm"))
            {
                return (float.Parse(value) / 10f).ToString();
            }
            // Se o valor original contém in (polegadas), converte para cm
            else if (originalValue.Contains("in"))
            {
                return (float.Parse(value) * 2.54f).ToString();
            }
            return value;
        }
        catch
        {
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
} 