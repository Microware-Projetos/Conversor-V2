using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Collections.Generic;
using eCommerce.Shared.Models;

namespace eCommerce.Server.Helpers
{
    public static class ProductUtils
    {
        public static string ProcessWeight(string weight, object product_attributes)
        {
            // Lógica simplificada, pode ser expandida conforme DataUtilsHP
            if (product_attributes != null)
            {
                var jsonData = JObject.FromObject(product_attributes);
                var dataNode = jsonData["data"];
                if (dataNode != null && dataNode.Type == JTokenType.Object)
                {
                    var productsNode = dataNode["products"];
                    if (productsNode != null && productsNode.Type == JTokenType.Object)
                    {
                        var products = productsNode as JObject;
                        var firstSku = products?.Properties().FirstOrDefault()?.Name;
                        if (!string.IsNullOrEmpty(firstSku))
                        {
                            var productWeightNode = products[firstSku];
                            if (productWeightNode != null && productWeightNode.Type == JTokenType.Array)
                            {
                                var productWeight = productWeightNode as JArray;
                                var weightFields = new[] { "Peso com embagem", "Package weight", "Peso", "Weight" };
                                foreach (var weightItem in productWeight)
                                {
                                    if (weightItem != null && weightItem.Type == JTokenType.Object)
                                    {
                                        var weightObj = weightItem as JObject;
                                        var name = weightObj["name"]?.ToString();
                                        var value = weightObj["value"]?.ToString();
                                        if (weightFields.Contains(name) && !string.IsNullOrWhiteSpace(value))
                                        {
                                            var originalValue = value.ToLower();
                                            if (!originalValue.Contains("kg") && !originalValue.Contains("lb"))
                                                continue;
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
            return weight;
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
                    var numbers = Regex.Matches(cleanValue, @"[-+]?\d*\.\d+|\d+");
                    if (numbers.Count > 0)
                    {
                        var numberValue = numbers[0].Value;
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
            value = Regex.Replace(value, @"\([^)]*\)", "");
            value = value.Split(';')[0].Trim();
            var sentences = value.Split('.');
            if (sentences.Length > 1)
            {
                var firstSentence = sentences[0].Trim();
                if (!string.IsNullOrEmpty(firstSentence) && (firstSentence.Contains("kg") || firstSentence.Contains("lb")))
                {
                    value = firstSentence;
                }
            }
            value = value.Replace("kg", "").Replace("lb", "").Trim();
            value = value.Replace(",", ".");
            value = string.Join(" ", value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            return value;
        }

        private static string ConvertToKg(string value, string originalValue)
        {
            try
            {
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

        public static Dimensions ProcessDimensions(string dimensions, object dimensions_data = null)
        {
            try
            {
                if (dimensions_data != null)
                {
                    var jsonData = JObject.FromObject(dimensions_data);
                    var dataNode = jsonData["data"];
                    if (dataNode != null && dataNode.Type == JTokenType.Object)
                    {
                        var productsNode = dataNode["products"];
                        if (productsNode != null && productsNode.Type == JTokenType.Object)
                        {
                            var products = productsNode as JObject;
                            var firstSku = products?.Properties().FirstOrDefault()?.Name;
                            if (!string.IsNullOrEmpty(firstSku))
                            {
                                var productDimsNode = products[firstSku];
                                if (productDimsNode != null && productDimsNode.Type == JTokenType.Array)
                                {
                                    var productDims = productDimsNode as JArray;
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
                                    foreach (var dimItem in productDims)
                                    {
                                        if (dimItem != null && dimItem.Type == JTokenType.Object)
                                        {
                                            var dimObj = dimItem as JObject;
                                            var name = dimObj["name"]?.ToString();
                                            var value = dimObj["value"]?.ToString();
                                            var isDimensionField = dimensionFields.Contains(name) ||
                                                (name != null && (name.ToLower().Contains("dimension") || name.ToLower().Contains("dimensão")));
                                            if (isDimensionField && !string.IsNullOrWhiteSpace(value))
                                            {
                                                var originalValue = value.ToLower();
                                                if (!originalValue.Contains("cm") && !originalValue.Contains("mm") && !originalValue.Contains("in"))
                                                    continue;
                                                var result = ProcessThreeDimensions(originalValue);
                                                if (result != null)
                                                    return result;
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
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar dimensões da API para produto: {ex.Message}");
            }
            // Fallback para o processamento original
            return null;
        }

        public static List<MetaData> ProcessarFotos(string sku, string model, List<object> images, Dictionary<string, string> normalizedFamily, object productAttributesAPI, string sheetName)
        {
            var metaData = new List<MetaData>();
            try
            {
                // 1. PRIMEIRA TENTATIVA: ProcessarFotosAlternativo (busca por família/categoria específica)
                var fotosAlternativo = new List<MetaData>(); // Chamar ProcessarFotosAlternativo se implementado
                if (fotosAlternativo.Count > 0)
                    return fotosAlternativo;
                // 2. SEGUNDA TENTATIVA: Cache de Produtos
                var fotosCache = new List<MetaData>(); // Chamar VerificarFotoNoCache se implementado
                if (fotosCache.Count > 0)
                    return fotosCache;
                // 3. TERCEIRA TENTATIVA: API HP Atual
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
                                            if (imageUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                                imageUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                            {
                                                metaData.Add(new MetaData
                                                {
                                                    key = "_external_image_url",
                                                    value = imageUri
                                                });
                                                return metaData;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                // 4. QUARTA TENTATIVA: Fotos Default (última tentativa)
                var fotosDefault = new List<MetaData>(); // Chamar ProcessarFotosAlternativo se implementado
                if (fotosDefault.Count > 0)
                    return fotosDefault;
                return metaData;
            }
            catch (Exception ex)
            {
                // Se houver erro, tenta usar o método alternativo de busca de imagens
                return new List<MetaData>(); // Chamar ProcessarFotosAlternativo se implementado
            }
        }

        // Adicione as funções auxiliares necessárias:
        private static Dimensions? ProcessThreeDimensions(string value)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(value, @"[-+]?\d*\.?\d+");
            if (matches.Count >= 3)
            {
                var l = matches[0].Value.Replace(",", ".");
                var w = matches[1].Value.Replace(",", ".");
                var h = matches[2].Value.Replace(",", ".");
                return new Dimensions { length = l, width = w, height = h };
            }
            return null;
        }

        private static Dimensions? ProcessSingleDimension(string value)
        {
            var match = System.Text.RegularExpressions.Regex.Match(value, @"[-+]?\d*\.?\d+");
            if (match.Success)
            {
                var w = match.Value.Replace(",", ".");
                return new Dimensions { width = w };
            }
            return null;
        }
    }
} 