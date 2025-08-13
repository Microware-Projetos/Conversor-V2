using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using ClosedXML.Excel;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using eCommerce.Server.Helpers;
using eCommerce.Shared.Models;

namespace eCommerce.Server.Processors.Lenovo.Helpers;

public static class ProductDataUtilsLenovo
{
    private static readonly string CATEGORIES_PATH_FILE ="/app/eCommerce/Server/Maps/Lenovo/categoriesWordpress.json";
    private static readonly HttpClient _httpClient = new HttpClient();
    private static readonly Dictionary<int, string> DEFAULT_PHOTOS = new()
        {
            { 17, "https://eprodutos-integracao.microware.com.br/api/photos/image/67f027b0ff6d81181139ddfe.png" },  // Acessório
            { 18, "https://eprodutos-integracao.microware.com.br/api/photos/image/67c1e5d5be14dc12f6b266dc.png" },  // Desktop
            { 19, "https://eprodutos-integracao.microware.com.br/api/photos/image/67c1e601be14dc12f6b266de.png" },  // Monitor
            { 20, "https://eprodutos-integracao.microware.com.br/api/photos/image/67c1e621be14dc12f6b266e0.png" },  // Notebook
            { 21, "https://eprodutos-integracao.microware.com.br/api/photos/image/67c1e66abe14dc12f6b266e2.png" },  // Serviços
            { 22, "https://eprodutos-integracao.microware.com.br/api/photos/image/67c1e688be14dc12f6b266e4.png" },  // SmartOffice
            { 23, "https://eprodutos-integracao.microware.com.br/api/photos/image/67c1e6bbbe14dc12f6b266e8.png" },  // Workstation
            { 24, "https://eprodutos-integracao.microware.com.br/api/photos/image/67c1e69dbe14dc12f6b266e6.png" }   // Tiny
        };

    public static string ProcessWeight(IXLRow product, Dictionary<string, string> deliveryInfo, object productData)
    {
        if (productData != null)
            return ProcessApiWeight(productData);
        else
        {
            try
            {
                var normalizedFamily = NormalizeUtisLenovo.NormalizeValuesList("Familia");
                var ph4Description = product.Cell(5).Value.ToString() ?? "";
                var family = normalizedFamily.ContainsKey(ph4Description) 
                ? normalizedFamily[ph4Description]
                : "";
                
                if (deliveryInfo.TryGetValue(family, out var weight))
                {
                    return weight;
                }
                else
                    return "1.0";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar peso: {ex.Message}");
                return "1.0";
            }
        }
    }

    private static string ProcessApiWeight(object productData)
    {
        if (productData == null)
        {
            Console.WriteLine("Produto não encontrado na API Lenovo");
            return "1.0";
        }
        
        try
        {
            Dictionary<string, object> productDict = null;

            if (productData is JObject jProduct)
            {
                Console.WriteLine("[INFO] Convertendo JObject para Dictionary<string, object>");
                productDict = jProduct.ToObject<Dictionary<string, object>>();
            }
            else if (productData is Dictionary<string, object> dict)
            {
                productDict = dict;
            }
            else
            {
                Console.WriteLine("[ERRO] Tipo inesperado para productData: " + productData.GetType().Name);
                return "1.0";
            }

            if (!productDict.TryGetValue("data", out var dataObj))
            {
                Console.WriteLine("[ERRO]: Chave 'data' NÃO encontrada em productDict");
                return "1.0";
            }

            Dictionary<string, object> dataDict = null;

            if (dataObj is JObject jData)
            {
                dataDict = jData.ToObject<Dictionary<string, object>>();
            }
            else if (dataObj is Dictionary<string, object> dictData)
            {
                dataDict = dictData;
            }
            else
            {
                Console.WriteLine("[ERRO]: 'data' NÃO é Dictionary<string, object>");
                Console.WriteLine("Tipo real de dataObj: " + dataObj.GetType());
                return "1.0";
            }

            if (!dataDict.TryGetValue("SpecData", out var specDataObj))
            {
                Console.WriteLine("[ERRO] Chave 'SpecData' não encontrada.");
                return "1.0";
            }

            List<object> specData = null;

            if (specDataObj is JArray jArray)
            {
                specData = jArray.ToObject<List<object>>();
            }
            else if (specDataObj is List<object> list)
            {
                specData = list;
            }
            else
            {
                Console.WriteLine("[ERRO] 'SpecData' não é uma lista reconhecida.");
                Console.WriteLine("Tipo real de SpecData: " + specDataObj.GetType());
                return "1.0";
            }

            Dictionary<string, object> weightEntry = null;

            foreach (var item in specData)
            {
                Dictionary<string, object> dictItem = null;

                if (item is JObject jItem)
                    dictItem = jItem.ToObject<Dictionary<string, object>>();
                else if (item is Dictionary<string, object> di)
                    dictItem = di;

                if (dictItem == null)
                    continue;

                if (dictItem.TryGetValue("name", out var nameVal) &&
                    nameVal?.ToString() == "Weight")
                {
                    weightEntry = dictItem;
                    break;
                }
            }

            if (weightEntry == null ||
                !weightEntry.TryGetValue("content", out var contentObj))
            {
                Console.WriteLine("[ERRO] Campo 'Weight' não encontrado ou sem conteúdo.");
                return "1.0";
            }

            // Tratamento para contentList
            List<object> contentList = null;

            if (contentObj is JArray jArrayContent)
                contentList = jArrayContent.ToObject<List<object>>();
            else if (contentObj is List<object> listContent)
                contentList = listContent;

            if (contentList == null)
            {
                Console.WriteLine("[ERRO] 'content' não é uma lista reconhecida.");
                return "1.0";
            }

            // Tratmento com múltiplos pesos
            if (contentList.Count > 1)
            {
                var firstWeightText = contentList[0]?.ToString();
                var weightPart = firstWeightText.Split('|').Last().Trim();
                var extracted = ExtractWeight(weightPart);

                if (extracted != null)
                    return extracted;
            }
            else
            {
                // Tratamento com um único peso
                var weightText = contentList[0]?.ToString();

                if (weightText.Contains("+"))
                {
                    double weight = 0;
                    foreach (var part in weightText.Split('+'))
                    {
                        var w = ExtractWeight(part);
                        if (double.TryParse(w, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                            weight += val;
                    }
                    if (weight > 0)
                        return weight.ToString(CultureInfo.InvariantCulture);
                }
                
                var extracted = ExtractWeight(weightText);
                if (extracted != null)
                    return extracted;
            }

            Console.WriteLine("Peso não encontrado ou inválido na API Lenovo");
            return "1.0";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao processar peso da API Lenovo: {ex.Message}");
            return "1.0";
        }
    }

    private static string ExtractWeight(string weightText)
    {
        if (string.IsNullOrEmpty(weightText))
            return null;
        
        // Remove símbolos de marca registrada e outros caracteres não numéricos
        var clean = Regex.Replace(weightText, "[®™]", "", RegexOptions.IgnoreCase);

        // Regex de kg e g
        var kgMatch = Regex.Match(clean, @"(\d+(?:\.\d+)?)\s*kg", RegexOptions.IgnoreCase);
        if (kgMatch.Success)
            return double.Parse(kgMatch.Groups[1].Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        
        var gMatch = Regex.Match(clean, @"(\d+(?:\.\d+)?)\s*g", RegexOptions.IgnoreCase);
        if (gMatch.Success)
        {
            var grams = double.Parse(gMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            return (grams / 1000).ToString(CultureInfo.InvariantCulture);
        }

        return null;

    }

    public static Dimensions ProcessDimensions(IXLRow product, Dictionary<string, string> deliveryInfo, object productData)
    {
        if (productData != null)
            Console.WriteLine("[INFO]: Indo para ProcessApiDimensions");
            return ProcessApiDimensions(productData);

        try
        {
            var normalizedFamily = NormalizeUtisLenovo.NormalizeValuesList("Familia");
            var ph4Description = product.Cell(5).Value.ToString() ?? "";
            var family = normalizedFamily.ContainsKey(ph4Description)
                ? normalizedFamily[ph4Description]
                : "";

            if (deliveryInfo.TryGetValue("family_code", out var familyCode) && familyCode == family)
            {
                Console.WriteLine("[INFO]: Dentro do If deliveryInfo.TryGetValue do ProcessDimensions");
                return new Dimensions
                {
                    length = deliveryInfo.ContainsKey("depth") ? deliveryInfo["depth"] : "2.1",
                    width = deliveryInfo.ContainsKey("width") ? deliveryInfo["width"] : "2.1",
                    height = deliveryInfo.ContainsKey("height") ? deliveryInfo["height"] : "2.1"
                };
            }
            else
            {
                var productCode = product.Cell(3).Value.ToString();
                Console.WriteLine($"[INFO]: Produto '{productCode}' sem dimensões.");
                return DefaultDimensions();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO]: Processamento de informações de entrega: {ex.Message}");
            return DefaultDimensions();
        }
    }

    private static Dimensions ProcessApiDimensions(object productData)
    {
        if (productData == null)
        {
            Console.WriteLine("[ERRO]: Produto não encontrado na API Lenovo. Retorno dimensões padrões.");
            return DefaultDimensions();
        }

        try
        {
            var productDict = ConvertToDict(productData);
            if (!productDict.TryGetValue("data", out var dataObj))
            {
                Console.WriteLine("[ERRO]: A chave 'data' não foi encontrada em productDict.");
                return DefaultDimensions();
            }

            var dataDict = ConvertToDict(dataObj);

            if (!dataDict.TryGetValue("SpecData", out var specDataObj))
            {
                Console.WriteLine("[ERRO]: A chave 'SpecData' não foi encontrada em dataDict.");
                return DefaultDimensions();
            }

            Console.WriteLine("[INFO]: Criando lista specData = null");
            List<object> specData = null;

            Console.WriteLine("[INFO]: Verificando se o specDataObj é um JArray");
            if (specDataObj is JArray jSpecArray)
            {
                specData = jSpecArray.ToObject<List<object>>();
            }
            else if (specDataObj is List<object> list)
            {
                Console.WriteLine("[INFO]: specDataObj é um List<object>");
                specData = list;
            }
            else
            {
                Console.WriteLine($"[ERRO] Tipo inesperado de 'SpecData': {specDataObj?.GetType().FullName}");
                return DefaultDimensions();
            }


            Dimensions? ExtractFromEntry(Dictionary<string, object> entry)
            {
                Console.WriteLine("[INFO]: Iniciando ExtractFromEntry");
                if (entry == null)
                {
                    Console.WriteLine("[ERRO]: entry é null.");
                    return null;
                }

                Console.WriteLine("[INFO]: Verificando chave 'content' em entry");
                if (!entry.TryGetValue("content", out var contentObj))
                {
                    Console.WriteLine("[ERRO]: Chave 'content' não encontrada em entry.");
                    return null;
                }

                Console.WriteLine("[INFO]: Criando contentList");
                List<object> contentList = null;

                if (contentObj is JArray jArray)
                {
                    Console.WriteLine("[INFO]: Variável contentList recebe jArray.ToObject<List<object>>()");
                    contentList = jArray.ToObject<List<object>>();
                }
                else if (contentObj is List<object> innerList)
                {
                    Console.WriteLine("[INFO]: contentObj é um List<object>");
                    contentList = innerList;
                }
                else
                {
                    Console.WriteLine($"[ERRO]: Tipo inesperado de 'content': {contentObj?.GetType().FullName}");
                    return null;
                }

                if (contentList == null || contentList.Count == 0)
                {
                    Console.WriteLine("[ERRO]: Lista de conteúdo vazia ou nula.");
                    return null;
                }

                Console.WriteLine("[INFO]: Criando string dimensionText");
                string dimensionText = contentList.Count > 1
                    ? contentList.Last().ToString().Split('|').Last().Trim()
                    : contentList[0].ToString();

                Console.WriteLine("[INFO]: Retornando ExtractDimensions passando dimensionText");
                return ExtractDimensions(dimensionText);
            }

            // Primeiro tenta encontrar "Packaging Dimensions (WxDxH)"
            Console.WriteLine("[INFO]: Procurando por 'Packaging Dimensions (WxDxH)'");
            Console.WriteLine("[INFO]: Criando packingEntry");
            
            Dictionary<string, object>? packagingEntry = null;
            foreach (var item in specData)
            {
                if (item is not JObject jObject)
                {
                    Console.WriteLine("[WARNING]: Item em specData não é JObject");
                    continue;
                }

                var dict = jObject.ToObject<Dictionary<string, object>>();
                
                if (dict == null)
                {
                    Console.WriteLine("[ERRO]: A variável dict está null");
                    continue;
                }

                if (dict.TryGetValue("name", out var nameObj) && nameObj?.ToString() == "Packaging Dimensions (WxDxH)")
                {
                    Console.WriteLine("[INFO]: Dentro do if dict.TryGetValue('name', out var nameObj) && nameObj?.ToString() == Packaging Dimensions (WxDxH)");
                    Console.WriteLine("[INFO]: A variável packagingEntry recebe dict");
                    packagingEntry = dict;
                    break;
                }
            }

            Console.WriteLine("[INFO]: Criando packingDims");
            var packagingDims = ExtractFromEntry(packagingEntry);
            if (packagingDims != null)
            {
                Console.WriteLine("[INFO]: PackingDims não é null, retornando o mesmo.");
                return packagingDims;
            }

            // Depois tenta "Dimensions (WxDxH)"
            Console.WriteLine("[INFO]: Criando productEntry");
            Dictionary<string, object>? productEntry = null;
            foreach (var item in specData)
            {
                if (item is not JObject jObject)
                {
                    Console.WriteLine("[WARN]: Item em specData não é JObject");
                    continue;
                }

                var dict = jObject.ToObject<Dictionary<string, object>>();
                if (dict == null)
                {
                    Console.WriteLine("[ERRO]: Dentro do if dict está null");
                    continue;
                }

                if (dict.TryGetValue("name", out var nameObj) && nameObj?.ToString() == "Dimensions (WxDxH)")
                {
                    Console.WriteLine("[INFO]: Dentro do if productEntry recebe dict");
                    productEntry = dict;
                    break;
                }
            }

            Console.WriteLine("[INFO]: Criando productDims");

            var productDims = ExtractFromEntry(productEntry);
            if (productDims != null)
                return productDims;

            Console.WriteLine("[ERRO]: Dimensões não encontradas ou inválidas na API Lenovo");
            return DefaultDimensions();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO]: Processamento da API Lenovo: {ex.Message}");
            return DefaultDimensions();
        }
    }


    private static Dimensions? ExtractDimensions(string text)
    {
        var cleanText = Regex.Replace(text, "[®™]", "", RegexOptions.IgnoreCase);
        var pattern = @"(\d+\.?\d*)\s*x\s*(\d+\.?\d*)\s*x\s*(\d+\.?\d*)\s*mm";
        var match = Regex.Match(cleanText, pattern, RegexOptions.IgnoreCase);

        if (match.Success)
        {
            var width = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) / 10;
            var depth = float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) / 10;
            var height = float.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) / 10;

            return new Dimensions
            {
                width = width.ToString("0.0", CultureInfo.InvariantCulture),
                length = depth.ToString("0.0", CultureInfo.InvariantCulture),
                height = height.ToString("0.0", CultureInfo.InvariantCulture)
            };
        }

        return null;
    }

    private static Dimensions DefaultDimensions()
    {
        return new Dimensions { length = "2.1", width = "2.1", height = "2.1" };
    }

    private static Dictionary<string, object> ConvertToDict(object obj)
    {
        return obj is JObject jObj
            ? jObj.ToObject<Dictionary<string, object>>()
            : (Dictionary<string, object>)obj;
    }

    public static List<Category> ProcessCategories(IXLRow product)
    {
        var CATEGORY_MAPPING = new Dictionary<string, List<string>>
        {
            { "Notebook", new List<string> { "Notebook" } },
            { "Desktop", new List<string> { "Desktop" } },
            { "Workstation", new List<string> { "Workstation" } },
            { "Tablet Android", new List<string> { "Tablet" } },
            { "Visuals", new List<string> { "Display" } },
            { "Smart Office", new List<string> { "Equipamento para Reunião" } }
        };

        // Obtém o valor de PH_BRAND (coluna desejada da planilha)
        string phBrand = product.Cell(4).Value.ToString();
        var categoryNames = CATEGORY_MAPPING.ContainsKey(phBrand)
            ? CATEGORY_MAPPING[phBrand]
            : new List<string> { "Acessórios" };
        
        var json = File.ReadAllText(CATEGORIES_PATH_FILE);
        var categoriesMapping = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);

        var categories = new List<Category>();

        foreach (var categoryName in categoryNames)
        {
            var matched = categoriesMapping
                .FirstOrDefault(cat => cat.TryGetValue("name", out var name) && name?.ToString() == categoryName);

            if (matched != null && matched.TryGetValue("id", out var idObj) && int.TryParse(idObj.ToString(), out var id))
            {
                categories.Add(new Category { id = id });
            }
        }

        if(!categories.Any())
        {
            var fallback = categoriesMapping
                .FirstOrDefault(cat => cat.TryGetValue("name", out var name) && name?.ToString() == "Acessório");
            
            if (fallback != null && fallback.TryGetValue("id", out var fallbackIdObj) &&
                int.TryParse(fallbackIdObj.ToString(), out var fallbackId))
            {
                categories.Add(new Category { id = fallbackId});
            }
        }

        return categories;
    }

    public static async Task<List<MetaData>> ProcessPhotos(IXLRow product, List<Dictionary<string, object>> images, Dictionary<string, string> normalizedFamily, object productData)
    {
        Console.WriteLine("[INFO]: Iniciando ProcessPhotos");
        var metaData = new List<MetaData>();

        if (productData != null)
        {
            Console.WriteLine("[INFO]: Product Data não é nullo, indo para FindApiImages.");
            return await FindApiImages(product, productData);
        }

        string ph4Desc = product.Cell(5).GetString();
        string phBrand = product.Cell(4).GetString();
        string productCode = product.Cell(3).GetString();
        string familyNormalized = normalizedFamily.ContainsKey(ph4Desc) ? normalizedFamily[ph4Desc] : "";

        var baseUrl = "https://eprodutos-integracao.microware.com.br/api/photos/image/";

        var filtered = images
            .Where(img =>
                img.TryGetValue("family", out var familyObj) &&
                familyObj != null &&
                ph4Desc.Contains(familyObj.ToString()))
            .ToList();

        if (!filtered.Any())
        {
            Console.WriteLine("[INFO]: Dentro do filtered.Any()");

            filtered = images
                .Where(img =>
                    img.TryGetValue("manufacturer", out var manufacturer) && manufacturer?.ToString() == "Lenovo" &&
                    img.TryGetValue("category", out var category) && category?.ToString() == phBrand &&
                    img.TryGetValue("family", out var fam) && fam?.ToString() == familyNormalized)
                .ToList();
        }

        if (!filtered.Any())
        {
            var CATEGORY_MAPPING = new Dictionary<string, List<string>>
            {
                { "Notebook", new List<string> { "Notebook" } },
                { "Desktop", new List<string> { "Desktop" } },
                { "Workstation", new List<string> { "Workstation" } },
                { "Tablet Android", new List<string> { "Tablet" } },
                { "Visuals", new List<string> { "Monitor" } },
                { "Smart Office", new List<string> { "SmartOffice" } }
            };

            var categoriasPadrao = CATEGORY_MAPPING.ContainsKey(phBrand) ? CATEGORY_MAPPING[phBrand] : new List<string> { "Acessório" };

            filtered = images
                .Where(img =>
                    img.TryGetValue("category", out var cat) && categoriasPadrao.Contains(cat?.ToString()) &&
                    img.TryGetValue("manufacturer", out var manuf) && manuf?.ToString() == "Lenovo" &&
                    img.TryGetValue("family", out var fam) && fam?.ToString() == "Default")
                .ToList();

            if (filtered.Any())
                Console.WriteLine($"[INFO]: {productCode} - Usando foto default");
        }

        var imageUrls = filtered
            .Where(img =>
                img.TryGetValue("id", out var id) &&
                img.TryGetValue("extension", out var ext))
            .Select(img => $"{baseUrl}{img["id"]}.{img["extension"]}")
            .ToList();

        if (!imageUrls.Any())
        {
            var categories = ProductDataUtilsLenovo.ProcessCategories(product);
            var defaultPhoto = GetDefaultPhoto(categories);
            metaData.Add(new MetaData
            {
                key = "_external_image_url",
                value = defaultPhoto
            });
            Console.WriteLine($"[INFO]: {productCode} - Usando foto padrão da categoria");
            return metaData;
        }

        metaData.Add(new MetaData
        {
            key = "_external_image_url",
            value = imageUrls.First()
        });

        if (imageUrls.Count > 1)
        {
            metaData.Add(new MetaData
            {
                key = "_external_gallery_images",
                value = imageUrls.Skip(1).ToList()
            });
        }

        return metaData;
    }

    private static async Task<List<MetaData>> FindApiImages(IXLRow product, object productData)
    {
        var categories = ProductDataUtilsLenovo.ProcessCategories(product);
        var defaultPhoto = GetDefaultPhoto(categories);

        if (productData == null)
        {
            Console.WriteLine("[INFO]: Product Data é null");

            Console.WriteLine("[INFO]: Entrando no FindApiImages");
            string baseUrl = "https://psref.lenovo.com/api/product/Photo/0";
            string modelCode = product.Cell(3).Value.ToString();

            string url = $"{baseUrl}?model_code={modelCode}";
            string token = "eyJ0eXAiOiJKV1QifQ.bjVTdWk0YklZeUc2WnFzL0lXU0pTeU1JcFo0aExzRXl1UGxHN3lnS1BtckI0ZVU5WEJyVGkvaFE0NmVNU2U1ZjNrK3ZqTEVIZ29nTk1TNS9DQmIwQ0pTN1Q1VytlY1RpNzZTUldXbm4wZ1g2RGJuQWg4MXRkTmxKT2YrOW9LRjBzQUZzV05HM3NpcU92WFVTM0o0blM1SDQyUlVXNThIV1VBS2R0c1B2NjJyQjIrUGxNZ2x6RTRhUjY5UDZWclBX.ZDBmM2EyMWRjZTg2N2JmYWMxZDIxY2NiYjQzMWFhNjg1YjEzZTAxNmU2M2RmN2M5ZjIyZWJhMzZkOWI1OWJhZg";

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("x-psref-user-token", token);
            
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
            }

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(url);

                Console.WriteLine($"[INFO]: Status da resposta: {response.StatusCode}");

                if(response.IsSuccessStatusCode)
                {
                    var metaData = new List<MetaData>();
                    string content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[INFO]: Conteúdo da resposta: {content}");

                    var responseJObj = JObject.Parse(content);
                    var imageUrls = responseJObj["data"]?["ProductPicturePathArray"]?.ToObject<List<string>>() ?? new List<string>();

                    if (!imageUrls.Any())
                    {
                        Console.WriteLine("[INFO]: Nenhuma imagem retornada pela API.");
                        return new List<MetaData>
                        {
                            new MetaData { key = "_external_image_url", value = defaultPhoto }
                        };
                    }

                    metaData.Add(new MetaData
                    {
                        key = "_external_image_url",
                        value = imageUrls.First()
                    });

                    if (imageUrls.Count > 1)
                    {
                        metaData.Add(new MetaData
                        {
                            key = "_external_gallery_images",
                            value = imageUrls.Skip(1).ToList()
                        });
                    }
                    return metaData;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERRO]: Não foi possível realizar a requisição: {ex.Message}");
            }
            
            Console.WriteLine("[INFO]: Retornando vazio sem entrar em nada!");
            return new List<MetaData>
            {
                new MetaData { key = "_external_image_url", value = defaultPhoto }
            };
        }

        try
        {
            var metaData = new List<MetaData>();
            

            if (productData is not JObject productJObj)
            {
                Console.WriteLine("[INFO]: Product Data não é um JObject");
                return new List<MetaData>
                { 
                    new MetaData { key = "_external_image_url", value = defaultPhoto }
                };
            }

            Console.WriteLine("[INFO]: Pegando o campo de imagens do cache");

            var dataToken = productJObj["data"];
            var imageUrlsToken = dataToken?["ProductPicturePathArray"];

            var imageUrls = imageUrlsToken?.ToObject<List<string>>() ?? new List<string>();

            Console.WriteLine($"[DEBUG]: {imageUrls.Count} imagens encontradas no cache.\n\n --------------- \n\n");

            if (!imageUrls.Any())
            {
                Console.WriteLine("[INFO]: Nenhuma imagem no cache. Usando imagem padrão.");
                return new List<MetaData>
                {
                    new MetaData { key = "_external_image_url", value = defaultPhoto }
                };
            }

            metaData.Add(new MetaData
            {
                key = "_external_image_url",
                value = imageUrls.First()
            });

            if (imageUrls.Count > 1)
            {
                metaData.Add(new MetaData
                {
                    key = "_external_gallery_images",
                    value = imageUrls.Skip(1).ToList()
                });
            }

            return metaData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO]: Falha ao processar imagens do cache: {ex.Message}");
            return new List<MetaData>
            {
                new MetaData { key = "_external_image_url", value = defaultPhoto }
            };
        }
    }

    private static string GetDefaultPhoto(List<Category> categories)
    {

        foreach (var category in categories)
        {
            if (DEFAULT_PHOTOS.TryGetValue(category.id, out var photoUrl))
            {
                return photoUrl;
            }
        }

        return DEFAULT_PHOTOS[17];
    }
}