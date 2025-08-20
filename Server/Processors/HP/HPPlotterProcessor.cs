using eCommerce.Shared.Models;
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

    public static class HPPlotterProcessor
    {
        
        //CONTINUAR O PROCESSAMENTO DE PLOTTERS

        public static async Task ProcessarListaPlotter(string caminhoArquivoProdutos, CancellationToken cancellationToken = default)
        {
            const int MARGIN = 20;
            const int STOCK = 10;

            try
            {
                // Verificar cancelamento
                cancellationToken.ThrowIfCancellationRequested();

                // Verifica se o arquivo existe
                if (!File.Exists(caminhoArquivoProdutos))
                {
                    throw new FileNotFoundException($"[INFO]: Arquivo de Plotter não encontrado: {caminhoArquivoProdutos}");
                }

                Console.WriteLine($"[INFO]: Arquivo de Plotter: {caminhoArquivoProdutos}");

                var produtos = new List<WooProduct>();
                var listProdutos = new XLWorkbook(caminhoArquivoProdutos);

                foreach (var worksheet in listProdutos.Worksheets)
                {
                    // Verificar cancelamento
                    cancellationToken.ThrowIfCancellationRequested();

                    string aba = worksheet.Name;
                    Console.WriteLine($"\n[INFO]: Processando aba: {aba}");

                    if (aba == "SP")
                    {
                        var linhas = worksheet.RowsUsed().Skip(6);
                        var cabecalho = worksheet.Row(6);
                        int contadorLinhas = 0;
                        bool isAcessory = false;

                        foreach (var linha in linhas)
                        {
                            // Verificar cancelamento
                            cancellationToken.ThrowIfCancellationRequested();

                            try
                            {
                                var sku = linha.Cell(2).Value.ToString() ?? "";
                                
                                Console.WriteLine($"[INFO]: Linha: {linha.Cell(1).Value.ToString()}");
                                if(linha.Cell(1).Value.ToString().Contains("Acessórios") || isAcessory)
                                {
                                    isAcessory = true;
                                }
                                else if(string.IsNullOrEmpty(sku))
                                {
                                    Console.WriteLine("[AVISO]: SKU vazio, pulando linha.");
                                    continue;
                                }

                                var productName = linha.Cell(3).Value.ToString() ?? "";
                                var shortDescription = linha.Cell(3).Value.ToString() ?? "";
                                var description = linha.Cell(3).Value.ToString() ?? "";
                                var price = "";
                                var regularPrice = "";
                                var stockQuantity = STOCK.ToString();
                                var attributes = await PlotterDataUtilsHP.ProcessAttributes(sku); // DEFINIR VALORES A SEREM PEGOS DO PLOTTER
                                var metaData = await PlotterDataUtilsHP.ProcessPhotos(sku);
                                var dimensions = isAcessory ? new Dimensions { length = "33.1", width = "40.8", height = "31.5" } : PlotterDataUtilsHP.ProcessDimensions(sku);
                                var weight = isAcessory ? "5.5" : PlotterDataUtilsHP.ProcessWeight(sku);
                                var categories = new List<Category> { new Category { id = 24 } };
                                var shippingClass = PlotterDataUtilsHP.ProcessShippingClass(linha);
                                var manageStock = true;

                                var descriptionAPI = await PlotterDataUtilsHP.ProcessDescription(sku);

                                try
                                {
                                    var strPrice = linha.Cell(20).Value.ToString();
                                    double doublePrice = double.Parse(strPrice);
                                    doublePrice = doublePrice / (1 - (MARGIN / 100));

                                    price = doublePrice.ToString("F2");
                                    regularPrice = doublePrice.ToString("F2");
                                    Console.WriteLine($"[INFO]: Preço {sku} calculado: {price}");
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[ERRO]: Erro ao converter preço | {ex.Message}");
                                        continue;
                                }

                                var produto = new WooProduct
                                {
                                    name = productName,
                                    sku = sku,
                                    short_description = shortDescription,
                                    description = description,
                                    price = price,
                                    regular_price = regularPrice,
                                    stock_quantity = stockQuantity,
                                    weight = weight,
                                    manage_stock = manageStock,
                                    attributes = attributes,
                                    dimensions = dimensions,
                                    categories = categories,
                                    shipping_class = shippingClass,
                                    meta_data = metaData
                                };

                                produtos.Add(produto);
                                contadorLinhas++;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ERRO]: Erro ao processar linha | {ex.Message}");
                                continue;
                            }
                        }
                    }
                    else
                        Console.WriteLine($"[INFO]: Aba não processada: {aba}");
                }

                if (produtos.Count > 0)
                {
                    //Salvar produtos em um json
                    var json = JsonConvert.SerializeObject(produtos, Formatting.Indented, new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.None,
                        NullValueHandling = NullValueHandling.Ignore,
                        ObjectCreationHandling = ObjectCreationHandling.Replace
                    });
                    var caminhoArquivoJson = Path.Combine(Directory.GetCurrentDirectory(), "plotterHP.json");
                    File.WriteAllText(caminhoArquivoJson, json);
                    Console.WriteLine($"\n[INFO]: Arquivo plotterHP.json criado com sucesso em: {caminhoArquivoJson}");
                    Console.WriteLine($"[INFO]: Tamanho do arquivo: {new FileInfo(caminhoArquivoJson).Length} bytes");
                }
                else
                {
                    Console.WriteLine("[ERRO]: Nenhum produto foi processado.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERRO]: Erro ao processar arquivo de Plotter: {ex.Message}");
            }

            Console.WriteLine($"\n[INFO]: Processando arquivo de produtos: {caminhoArquivoProdutos}");
        }
    }