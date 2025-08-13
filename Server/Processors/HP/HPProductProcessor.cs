using eCommerce.Shared.Models;
using WooAttribute = eCommerce.Shared.Models.Attribute;
using ClosedXML.Excel;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using System.Threading;
using eCommerce.Server.Helpers;
using eCommerce.Server.Processors.HP;
using eCommerce.Server.Processors.HP.Helpers;

namespace eCommerce.Server.Processors.HP;

public static class HPProductProcessor
{
    public static async Task ProcessarListasProdutos(string caminhoArquivoProdutos, string caminhoArquivoPrecos, CancellationToken cancellationToken = default)
    {
        const string CHACHE_DIR = "Cache/HP";

        try
        {

            // Verificar cancelamento
            cancellationToken.ThrowIfCancellationRequested();
            
            // Verifica se os arquivos existem
            if (!File.Exists(caminhoArquivoProdutos))
            {
                throw new FileNotFoundException($"Arquivo de produtos não encontrado: {caminhoArquivoProdutos}");
            }
            
            if (!File.Exists(caminhoArquivoPrecos))
            {
                throw new FileNotFoundException($"Arquivo de preços não encontrado: {caminhoArquivoPrecos}");
            }
            
            Console.WriteLine($"Arquivo de produtos: {caminhoArquivoProdutos}");
            Console.WriteLine($"Arquivo de preços: {caminhoArquivoPrecos}");
            
            // Garante que o diretório de cache existe
            CacheManagerHP.EnsureCacheDir(CHACHE_DIR);

            // Buscar imagens
            var images = await NormalizeUtisHP.BuscarImagens();

            // Buscar delivery
            var delivery = await NormalizeUtisHP.BuscarDelivery();

            // Normalizar valores
            var normalizedFamily = NormalizeUtisHP.NormalizeValuesList("Familia");
            var normalizedAnatel = NormalizeUtisHP.NormalizeValuesList("Anatel");
            
            var produtos = new List<WooProduct>();
            var listProdutos = new XLWorkbook(caminhoArquivoProdutos);
            var precosPorSku = ExcelProcessorHP.GetPrecosPorSku(caminhoArquivoPrecos);
            var leadtime = new Dictionary<float, string>
            {
                {0.04f, "importado"},
                {0.18f, "importado"},
                {0.07f, "local"},
                {0.12f, "local"} 
            };
            
            foreach (var worksheet in listProdutos.Worksheets)
            {
                // Verificar cancelamento a cada aba
                cancellationToken.ThrowIfCancellationRequested();
                
                string aba = worksheet.Name;
                Console.WriteLine($"Processando aba: {aba}");
                
                if (aba == "Desktops")
                {
                    var linhas = worksheet.RowsUsed().Skip(3);
                    var cabecalho = worksheet.Row(2); // Pega a segunda linha como cabeçalho
                    int contadorLinhas = 0;

                    foreach (var linha in linhas)
                    {
                        // Verificar cancelamento a cada linha
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        try
                        {
                            var sku = linha.Cell(2).Value.ToString() ?? "";
                            
                            if (!precosPorSku.ContainsKey(sku))
                            {
                                continue;
                            }
                            
                            var product_attributesAPI = await DataProcessorHP.GetAttributesBySKU(sku);

                            float preco = float.Parse(precosPorSku[sku]);

                            
                            var icms = precosPorSku[sku + "_icms"];
                            var lead = leadtime[float.Parse(icms)];
                            var ean = precosPorSku[sku + "_ean"];


                            if (preco == 0)
                            {
                                continue;
                            }

                            Console.WriteLine("ean: " + ean + " Tamanho: " + ean.Length);

                            // Preco com Margem
                            preco = preco / (1 - (20 / 100));
                        
                            var model = linha.Cell(3).Value.ToString() ?? "";
                            var processor = linha.Cell(4).Value.ToString() ?? "";
                            var os = linha.Cell(5).Value.ToString() ?? "";
                            var memory = linha.Cell(6).Value.ToString() ?? "";
                            var storage = linha.Cell(7).Value.ToString() ?? "";
                            var chipset = linha.Cell(8).Value.ToString() ?? "";
                            var discreteGraphic = linha.Cell(9).Value.ToString() ?? "";
                            var integratedGraphics = linha.Cell(10).Value.ToString() ?? "";
                            var opticalDrive = linha.Cell(11).Value.ToString() ?? "";
                            var keyboard = linha.Cell(12).Value.ToString() ?? "";
                            var mouse = linha.Cell(13).Value.ToString() ?? "";
                            var wirelessNIC = linha.Cell(14).Value.ToString() ?? "";
                            var display = linha.Cell(15).Value.ToString() ?? "";
                            var camera = linha.Cell(16).Value.ToString() ?? "";
                            var flexPort = linha.Cell(17).Value.ToString() ?? "";
                            var dimension = linha.Cell(20).Value.ToString() ?? "";
                            var weight = linha.Cell(21).Value.ToString() ?? "";

                            var prodInfo = new Dictionary<string, string>
                            {
                                { "EAN", ean },
                                { "sku", sku },
                                { "model", model }
                            };
                            
                            var attributes = AttributeProcessorHP.ProcessarAttributes(sku, model, linha, cabecalho, prodInfo, aba, normalizedAnatel, normalizedFamily);

                            // Processar fotos
                            var fotos = ProductDataUtilsHP.ProcessarFotos(sku, model, images, normalizedFamily, product_attributesAPI, aba);

                            var processedDimensions = ProductDataUtilsHP.ProcessDimensions(dimension, product_attributesAPI);

                            var produto = new WooProduct
                            {
                                name = "Desktop " + model,
                                sku = sku,
                                short_description = "HP " + model,
                                description = "Desktop " + model + " " + processor + " " + memory + " " + storage + " " + os,
                                price = preco.ToString(),
                                regular_price = preco.ToString(),
                                stock_quantity = "10",
                                weight = ProductDataUtilsHP.ProcessWeight(weight, product_attributesAPI),
                                manage_stock = true,
                                shipping_class = lead,
                                attributes = attributes,
                                dimensions = processedDimensions,
                                categories = new List<Category>{new Category{ id = 20}},
                                meta_data = fotos
                            };
                            
                            produtos.Add(produto);
                            contadorLinhas++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro ao processar linha: {ex.Message}");
                        }
                    }
                    
                    Console.WriteLine($"Processadas {contadorLinhas} linhas na aba Desktops");
                }

                if (aba == "Notebooks")
                {
                    var linhas = worksheet.RowsUsed().Skip(3);
                    var cabecalho = worksheet.Row(2); // Pega a segunda linha como cabeçalho
                    int contadorLinhas = 0;

                    foreach (var linha in linhas)
                    {
                        // Verificar cancelamento a cada linha
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        try
                        {
                            var sku = linha.Cell(2).Value.ToString() ?? "";
                            
                            if (!precosPorSku.ContainsKey(sku))
                            {
                                continue;
                            }
                            
                            var product_attributesAPI = await DataProcessorHP.GetAttributesBySKU(sku);

                            float preco = float.Parse(precosPorSku[sku]);

                            
                            var icms = precosPorSku[sku + "_icms"];
                            var lead = leadtime[float.Parse(icms)];
                            var ean = precosPorSku[sku + "_ean"];

                            if (preco == 0)
                            {
                                continue;
                            }

                            // Preco com Margem
                            preco = preco / (1 - (20 / 100));

                            var model = linha.Cell(3).Value.ToString() ?? "";
                            var processor = linha.Cell(4).Value.ToString() ?? "";
                            var os = linha.Cell(5).Value.ToString() ?? "";
                            var memory = linha.Cell(6).Value.ToString() ?? "";
                            var storage = linha.Cell(7).Value.ToString() ?? "";
                            var display = linha.Cell(8).Value.ToString() ?? "";
                            var color = linha.Cell(9).Value.ToString() ?? "";
                            var touch = linha.Cell(11).Value.ToString() ?? "";
                            var discreteGraphic = linha.Cell(12).Value.ToString() ?? "";
                            var integratedGraphics = linha.Cell(13).Value.ToString() ?? "";
                            var wirelessNIC = linha.Cell(14).Value.ToString() ?? "";
                            var battery = linha.Cell(17).Value.ToString() ?? "";
                            var dimension = linha.Cell(21).Value.ToString() ?? "";
                            var weight = linha.Cell(22).Value.ToString() ?? "";
                            
                            var prodInfo = new Dictionary<string, string>
                            {
                                { "EAN", ean },
                                { "sku", sku },
                                { "model", model }
                            };
                            
                            var attributes = AttributeProcessorHP.ProcessarAttributes(sku, model, linha, cabecalho, prodInfo, aba, normalizedAnatel, normalizedFamily);

                            // Processar fotos
                            var fotos = ProductDataUtilsHP.ProcessarFotos(sku, model, images, normalizedFamily, product_attributesAPI, aba);

                            var processedDimensions = ProductDataUtilsHP.ProcessDimensions(dimension, product_attributesAPI);

                            var produto = new WooProduct
                            {
                                name = "Notebook " + model,
                                sku = sku,
                                short_description = "HP " + model,
                                description = "Notebook " + model + " " + processor + " " + memory + " " + storage + " " + os,
                                price = preco.ToString(),
                                regular_price = preco.ToString(),
                                stock_quantity = "10",
                                weight = ProductDataUtilsHP.ProcessWeight(weight, product_attributesAPI),
                                manage_stock = true,
                                shipping_class = lead,
                                attributes = attributes,
                                dimensions = processedDimensions,
                                categories = new List<Category>{new Category{ id = 27}},
                                meta_data = fotos
                            };
                            
                            produtos.Add(produto);
                            contadorLinhas++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro ao processar linha: {ex.Message}");
                        }
                    }
                    
                    Console.WriteLine($"Processadas {contadorLinhas} linhas na aba Notebooks");
                }

                if (aba == "Mobile Workstation")
                {
                    var linhas = worksheet.RowsUsed().Skip(3);
                    var cabecalho = worksheet.Row(2); // Pega a segunda linha como cabeçalho
                    int contadorLinhas = 0;

                    foreach (var linha in linhas)
                    {
                        // Verificar cancelamento a cada linha
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        try
                        {
                            var sku = linha.Cell(2).Value.ToString() ?? "";
                            
                            if (!precosPorSku.ContainsKey(sku))
                            {
                                continue;
                            }
                            
                            var product_attributesAPI = await DataProcessorHP.GetAttributesBySKU(sku);

                            float preco = float.Parse(precosPorSku[sku]);

                            var icms = precosPorSku[sku + "_icms"];
                            var lead = leadtime[float.Parse(icms)];
                            var ean = precosPorSku[sku + "_ean"];

                            if (preco == 0)
                            {
                                continue;
                            }

                            // Preco com Margem
                            preco = preco / (1 - (20 / 100));
                            

                            var model = linha.Cell(3).Value.ToString() ?? "";
                            var processor = linha.Cell(4).Value.ToString() ?? "";
                            var os = linha.Cell(5).Value.ToString() ?? "";
                            var memory = linha.Cell(6).Value.ToString() ?? "";
                            var storage = linha.Cell(7).Value.ToString() ?? "";
                            var display = linha.Cell(9).Value.ToString() ?? "";
                            var discreteGraphic = linha.Cell(12).Value.ToString() ?? "";
                            var wirelessNIC = linha.Cell(13).Value.ToString() ?? "";
                            var battery = linha.Cell(14).Value.ToString() ?? "";
                            var dimension = linha.Cell(16).Value.ToString() ?? "";
                            var weight = linha.Cell(17).Value.ToString() ?? "";

                            var prodInfo = new Dictionary<string, string>
                            {
                                { "EAN", ean },
                                { "sku", sku },
                                { "model", model }
                            };
                            
                            var attributes = AttributeProcessorHP.ProcessarAttributes(sku, model, linha, cabecalho, prodInfo, aba, normalizedAnatel, normalizedFamily);

                            // Processar fotos
                            var fotos = ProductDataUtilsHP.ProcessarFotos(sku, model, images, normalizedFamily, product_attributesAPI, aba);

                            var processedDimensions = ProductDataUtilsHP.ProcessDimensions(dimension, product_attributesAPI);

                            var produto = new WooProduct
                            {
                                name = "Workstation " + model,
                                sku = sku,
                                short_description = "HP " + model,
                                description = "Workstation " + model + " " + processor + " " + memory + " " + storage + " " + os,
                                price = preco.ToString(),
                                regular_price = preco.ToString(),
                                stock_quantity = "10",
                                weight = ProductDataUtilsHP.ProcessWeight(weight, product_attributesAPI),
                                manage_stock = true,
                                shipping_class = lead,
                                attributes = attributes,
                                dimensions = processedDimensions,
                                categories = new List<Category>{new Category{ id = 39}},
                                meta_data = fotos
                            };
                            
                            produtos.Add(produto);
                            contadorLinhas++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro ao processar linha: {ex.Message}");
                        }
                    }
                    
                    Console.WriteLine($"Processadas {contadorLinhas} linhas na aba Mobile Workstation");
                }

                if (aba == "Workstations DT")
                {
                    var linhas = worksheet.RowsUsed().Skip(3);
                    var cabecalho = worksheet.Row(2); // Pega a segunda linha como cabeçalho
                    int contadorLinhas = 0;

                    foreach (var linha in linhas)
                    {
                        // Verificar cancelamento a cada linha
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        try
                        {
                            var sku = linha.Cell(2).Value.ToString() ?? "";
                            
                            if (!precosPorSku.ContainsKey(sku))
                            {
                                continue;
                            }
                            
                            var product_attributesAPI = await DataProcessorHP.GetAttributesBySKU(sku);

                            float preco = float.Parse(precosPorSku[sku]);
                           
                            
                            var icms = precosPorSku[sku + "_icms"];
                            var lead = leadtime[float.Parse(icms)];
                            var ean = precosPorSku[sku + "_ean"];

                            if (preco == 0)
                            {
                                continue;
                            }

                            // Preco com Margem
                            preco = preco / (1 - (20 / 100));

                            var model = linha.Cell(3).Value.ToString() ?? "";
                            var processor = linha.Cell(4).Value.ToString() ?? "";
                            var os = linha.Cell(5).Value.ToString() ?? "";
                            var memory = linha.Cell(6).Value.ToString() ?? "";
                            var storage = linha.Cell(7).Value.ToString() ?? "";
                            var discreteGraphic = linha.Cell(9).Value.ToString() ?? "";
                            var keyboard = linha.Cell(10).Value.ToString() ?? "";
                            var mouse = linha.Cell(11).Value.ToString() ?? "";
                            var dimension = linha.Cell(14).Value.ToString() ?? "";
                            var weight = linha.Cell(15).Value.ToString() ?? "";

                            var prodInfo = new Dictionary<string, string>
                            {
                                { "EAN", ean },
                                { "sku", sku },
                                { "model", model }
                            };
                            
                            var attributes = AttributeProcessorHP.ProcessarAttributes(sku, model, linha, cabecalho, prodInfo, aba, normalizedAnatel, normalizedFamily);

                            // Processar fotos
                            var fotos = ProductDataUtilsHP.ProcessarFotos(sku, model, images, normalizedFamily, product_attributesAPI, aba);

                            var processedDimensions = ProductDataUtilsHP.ProcessDimensions(dimension, product_attributesAPI);

                            var produto = new WooProduct
                            {
                                name = "Workstation " + model,
                                sku = sku,
                                short_description = "HP " + model,
                                description = "Workstation " + model + " " + processor + " " + memory + " " + storage + " " + os,
                                price = preco.ToString(),
                                regular_price = preco.ToString(),
                                stock_quantity = "10",
                                weight = ProductDataUtilsHP.ProcessWeight(weight, product_attributesAPI),
                                manage_stock = true,
                                shipping_class = lead,
                                attributes = attributes,
                                dimensions = processedDimensions,
                                categories = new List<Category>{new Category{ id = 39}},
                                meta_data = fotos
                            };
                            
                            produtos.Add(produto);
                            contadorLinhas++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro ao processar linha: {ex.Message}");
                        }
                    }
                    
                    Console.WriteLine($"Processadas {contadorLinhas} linhas na aba Workstations DT");
                }

                if (aba == "Thin Clients")
                {
                    var linhas = worksheet.RowsUsed().Skip(3);
                    var cabecalho = worksheet.Row(2); // Pega a segunda linha como cabeçalho
                    int contadorLinhas = 0;

                    foreach (var linha in linhas)
                    {
                        // Verificar cancelamento a cada linha
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        try
                        {
                            var sku = linha.Cell(2).Value.ToString() ?? "";
                            
                            if (!precosPorSku.ContainsKey(sku))
                            {
                                continue;
                            }
                            
                            var product_attributesAPI = await DataProcessorHP.GetAttributesBySKU(sku);

                            float preco = float.Parse(precosPorSku[sku]);

                            
                            var icms = precosPorSku[sku + "_icms"];
                            var lead = leadtime[float.Parse(icms)];
                            var ean = precosPorSku[sku + "_ean"];

                            if (preco == 0)
                            {
                                continue;
                            }

                            // Preco com Margem
                            preco = preco / (1 - (20 / 100));

                            var model = linha.Cell(3).Value.ToString() ?? "";
                            var os = linha.Cell(4).Value.ToString() ?? "";
                            var storage = linha.Cell(5).Value.ToString() ?? "";
                            var memory = linha.Cell(6).Value.ToString() ?? "";
                            var discreteGraphic = linha.Cell(7).Value.ToString() ?? "";
                            var keyboard = linha.Cell(9).Value.ToString() ?? "";
                            var mouse = linha.Cell(10).Value.ToString() ?? "";
                            var dimension = linha.Cell(50).Value.ToString() ?? "";
                            var weight = linha.Cell(50).Value.ToString() ?? "";

                            var prodInfo = new Dictionary<string, string>
                            {
                                { "EAN", ean },
                                { "sku", sku },
                                { "model", model }
                            };
                            
                            var attributes = AttributeProcessorHP.ProcessarAttributes(sku, model, linha, cabecalho, prodInfo, aba, normalizedAnatel, normalizedFamily);

                            // Processar fotos
                            var fotos = ProductDataUtilsHP.ProcessarFotos(sku, model, images, normalizedFamily, product_attributesAPI, aba);

                            var processedDimensions = ProductDataUtilsHP.ProcessDimensions(dimension, product_attributesAPI);

                            var produto = new WooProduct
                            {
                                name = "Desktop " + model,
                                sku = sku,
                                short_description = "HP " + model,
                                description = "Desktop " + model + " " + memory + " " + storage + " " + os,
                                price = preco.ToString(),
                                regular_price = preco.ToString(),
                                stock_quantity = "10",
                                weight = ProductDataUtilsHP.ProcessWeight(weight, product_attributesAPI),
                                manage_stock = true,
                                shipping_class = lead,
                                attributes = attributes,
                                dimensions = processedDimensions,
                                categories = new List<Category>{new Category{ id = 20}},
                                meta_data = fotos
                            };
                            
                            produtos.Add(produto);
                            contadorLinhas++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro ao processar linha: {ex.Message}");
                        }
                    }
                    
                    Console.WriteLine($"Processadas {contadorLinhas} linhas na aba Thin Clients");
                }

                if (aba == "SmartChoice")
                {
                    var linhas = worksheet.RowsUsed().Skip(3);
                    var cabecalho = worksheet.Row(2); // Pega a segunda linha como cabeçalho
                    int contadorLinhas = 0;

                    foreach (var linha in linhas)
                    {
                        // Verificar cancelamento a cada linha
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        try
                        {
                            var sku = linha.Cell(4).Value.ToString() ?? "";
                            
                            if (!precosPorSku.ContainsKey(sku))
                            {
                                continue;
                            }
                            
                            var product_attributesAPI = await DataProcessorHP.GetAttributesBySKU(sku);

                            float preco = float.Parse(precosPorSku[sku]);
                            var icms = precosPorSku[sku + "_icms"];
                            var lead = leadtime[float.Parse(icms)];
                            var ean = precosPorSku[sku + "_ean"];
                            var ncm = precosPorSku[sku + "_ncm"];

                            if (preco == 0)
                            {
                                continue;
                            }

                            // Preco com Margem
                            preco = preco / (1 - (20 / 100));

                            var descricao = linha.Cell(5).Value.ToString() ?? "";
                            var model = descricao; // Para SmartChoice, usa a descrição como model
                            var dimension = ""; // SmartChoice pode não ter dimensões
                            var weight = ""; // SmartChoice pode não ter peso

                            var prodInfo = new Dictionary<string, string>
                            {
                                { "EAN", ean },
                                { "sku", sku },
                                { "model", model }
                            };
                            
                            var attributes = AttributeProcessorHP.ProcessarAttributes(sku, model, linha, cabecalho, prodInfo, aba, normalizedAnatel, normalizedFamily);

                            // Processar fotos
                            var fotos = ProductDataUtilsHP.ProcessarFotos(sku, model, images, normalizedFamily, product_attributesAPI, aba);

                            var processedDimensions = ProductDataUtilsHP.ProcessDimensions(dimension, product_attributesAPI);

                            var produto = new WooProduct
                            {
                                name = descricao,
                                sku = sku,
                                short_description = descricao,
                                description = "Acessório " + descricao,
                                price = preco.ToString(),
                                regular_price = preco.ToString(),
                                stock_quantity = "10",
                                weight = ProductDataUtilsHP.ProcessWeight(weight, product_attributesAPI),
                                manage_stock = true,
                                shipping_class = lead,
                                attributes = attributes,
                                dimensions = processedDimensions,
                                categories = new List<Category>{new Category{ id = 16}},
                                meta_data = fotos
                            };
                            
                            produtos.Add(produto);
                            contadorLinhas++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro ao processar linha: {ex.Message}");
                        }
                    }

                    Console.WriteLine($"Processadas {contadorLinhas} linhas na aba SmartChoice");
                }

                if (aba == "Portfólio Acessorios_Monitores")
                {
                    var linhas = worksheet.RowsUsed().Skip(3);
                    var cabecalho = worksheet.Row(2); // Pega a segunda linha como cabeçalho
                    int contadorLinhas = 0;

                    foreach (var linha in linhas)
                    {
                        // Verificar cancelamento a cada linha
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        try
                        {
                            var sku = linha.Cell(3).Value.ToString() ?? "";
                            
                            if (!precosPorSku.ContainsKey(sku))
                            {
                                continue;
                            }
                            
                            var product_attributesAPI = await DataProcessorHP.GetAttributesBySKU(sku);

                            float preco = float.Parse(precosPorSku[sku]);
                            var icms = precosPorSku[sku + "_icms"];
                            var lead = leadtime[float.Parse(icms)];
                            var ean = precosPorSku[sku + "_ean"];
                            var ncm = precosPorSku[sku + "_ncm"];

                            if (preco == 0)
                            {
                                continue;
                            }

                            // Preco com Margem
                            preco = preco / (1 - (20 / 100));

                            var descricao = linha.Cell(4).Value.ToString() ?? "";
                            var tipo = linha.Cell(6).Value.ToString() ?? "";
                            var model = descricao; // Para Portfólio Acessorios_Monitores, usa a descrição como model
                            var dimension = ""; // Portfólio Acessorios_Monitores pode não ter dimensões
                            var weight = ""; // Portfólio Acessorios_Monitores pode não ter peso
                            var tipoCategoria = "";

                            int category = 0;
                            if (tipo.IndexOf("Display", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                category = 21;
                                tipoCategoria = "Display";
                            }
                            else
                            {
                                category = 16;
                                tipoCategoria = "Acessório";
                            }

                            var prodInfo = new Dictionary<string, string>
                            {
                                { "EAN", ean },
                                { "sku", sku },
                                { "model", model }
                            };
                            
                            var attributes = AttributeProcessorHP.ProcessarAttributes(sku, model, linha, cabecalho, prodInfo, aba, normalizedAnatel, normalizedFamily);

                            // Processar fotos
                            var fotos = ProductDataUtilsHP.ProcessarFotos(sku, model, images, normalizedFamily, product_attributesAPI, aba);

                            var processedDimensions = ProductDataUtilsHP.ProcessDimensions(dimension, product_attributesAPI);

                            var produto = new WooProduct
                            {
                                name = descricao,
                                sku = sku,
                                short_description = descricao,
                                description = tipoCategoria + " " + descricao,
                                price = preco.ToString(),
                                regular_price = preco.ToString(),
                                stock_quantity = "10",
                                weight = ProductDataUtilsHP.ProcessWeight(weight, product_attributesAPI),
                                manage_stock = true,
                                shipping_class = lead,
                                attributes = attributes,
                                dimensions = processedDimensions,
                                categories = new List<Category>{new Category{ id = category}},
                                meta_data = fotos
                            };
                            
                            produtos.Add(produto);
                            contadorLinhas++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro ao processar linha: {ex.Message}");
                        }
                    }

                    Console.WriteLine($"Processadas {contadorLinhas} linhas na aba SmartChoice");
                }
            }

            Console.WriteLine($"Total de produtos processados: {produtos.Count}");
            
            if (produtos.Count > 0)
            {
                //Salvar produtos em um json;
                var json = JsonConvert.SerializeObject(produtos, Formatting.Indented, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    NullValueHandling = NullValueHandling.Ignore,
                    ObjectCreationHandling = ObjectCreationHandling.Replace
                });
                var caminhoArquivoJson = Path.Combine(Directory.GetCurrentDirectory(), "produtosHP.json");
                File.WriteAllText(caminhoArquivoJson, json);
                Console.WriteLine($"Arquivo produtos.json criado com sucesso em: {caminhoArquivoJson}");
                Console.WriteLine($"Tamanho do arquivo: {new FileInfo(caminhoArquivoJson).Length} bytes");
            }
            else
            {
                Console.WriteLine("Nenhum produto foi processado.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro durante o processamento: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

  
}