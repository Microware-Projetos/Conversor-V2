using eCommerce.Shared.Models;
using WooAttribute = eCommerce.Shared.Models.Attribute;
using ClosedXML.Excel;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace eCommerce.Server.Processors.HP;

public static class HPProductProcessor
{
    public static async Task ProcessarListasProdutos(string caminhoArquivoProdutos, string caminhoArquivoPrecos)
    {
        try
        {
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
            CacheManagerHP.EnsureCacheDir();

            // Buscar imagens
            var images = await NormalizeUtis.BuscarImagens();

            // Buscar delivery
            var delivery = await NormalizeUtis.BuscarDelivery();

            // Normalizar valores
            var normalizedFamily = NormalizeUtis.NormalizeValuesList("Familia");
            var normalizedAnatel = NormalizeUtis.NormalizeValuesList("Anatel");
            
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
                string aba = worksheet.Name;
                Console.WriteLine($"Processando aba: {aba}");
                
                if (aba == "Desktops")
                {
                    var linhas = worksheet.RowsUsed().Skip(3);
                    var cabecalho = worksheet.Row(2); // Pega a segunda linha como cabeçalho
                    int contadorLinhas = 0;

                    foreach (var linha in linhas)
                    {
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
                            var fotos = DataUtilsHP.ProcessarFotos(sku, model, images, normalizedFamily, product_attributesAPI, aba);

                            var produto = new WooProduct
                            {
                                name = "Desktop " + model,
                                sku = sku,
                                short_description = "HP " + model,
                                description = "Desktop " + model + " " + processor + " " + memory + " " + storage + " " + os,
                                price = preco.ToString(),
                                regular_price = preco.ToString(),
                                stock_quantity = "10",
                                weight = DataUtilsHP.ProcessWeight(weight, product_attributesAPI),
                                manage_stock = true,
                                shipping_class = lead,
                                attributes = attributes,
                                dimensions = DataUtilsHP.ProcessDimensions(dimension),
                                categories = new List<Category>{new Category{ id = 20}},
                                meta_data = DataUtilsHP.ProcessarFotos(sku, model, images, normalizedFamily, product_attributesAPI, aba)
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

            }

            Console.WriteLine($"Total de produtos processados: {produtos.Count}");
            
            if (produtos.Count > 0)
            {
                //Salvar produtos em um json;
                var json = JsonConvert.SerializeObject(produtos, Formatting.Indented);
                var caminhoArquivoJson = Path.Combine(Directory.GetCurrentDirectory(), "produtos.json");
                File.WriteAllText(caminhoArquivoJson, json);
                Console.WriteLine($"Arquivo produtos.json criado com sucesso em: {caminhoArquivoJson}");
                Console.WriteLine($"Tamanho do arquivo: {new FileInfo(caminhoArquivoJson).Length} bytes");
            }
            else
            {
                Console.WriteLine("Nenhum produto foi processado. Verifique se a aba 'Desktops' existe e contém dados.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro durante o processamento: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }


}