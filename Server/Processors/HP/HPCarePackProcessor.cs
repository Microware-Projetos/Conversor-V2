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

public static class HPCarePackProcessor
{

    public static async Task ProcessarListaCarePack(string caminhoArquivoProdutos, CancellationToken cancellationToken = default)
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
                throw new FileNotFoundException($"[INFO]: Arquivo de CarePack não encontrado: {caminhoArquivoProdutos}");
            }

            Console.WriteLine($"[INFO]: Arquivo de CarePack: {caminhoArquivoProdutos}");

            var produtos = new List<WooProduct>();
            var listProdutos = new XLWorkbook(caminhoArquivoProdutos);

            foreach (var worksheet in listProdutos.Worksheets)
            {
                // Verificar cancelamento
                cancellationToken.ThrowIfCancellationRequested();

                string aba = worksheet.Name;
                Console.WriteLine($"\n[INFO]: Processando aba: {aba}");

                if (aba == "Channel")
                {
                    var linhas = worksheet.RowsUsed().Skip(3);
                    var cabecalho = worksheet.Row(3);
                    int contadorLinhas = 0;

                    foreach (var linha in linhas)
                    {
                        // Verificar cancelamento
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            var sku = linha.Cell(8).Value.ToString() ?? "";
                            var productName = linha.Cell(9).Value.ToString() ?? "";
                            var shortDescription = linha.Cell(9).Value.ToString() ?? "";
                            var description = linha.Cell(9).Value.ToString() ?? "";
                            var price = "";
                            var regularPrice = "";
                            var stockQuantity = STOCK.ToString();
                            var attributes = CarePackDataUtilsHP.ProcessAttributes(linha); 
                            var metaData = CarePackDataUtilsHP.ProcessPhotos();
                            var dimensions = CarePackDataUtilsHP.ProcessDimensions();
                            var weight = CarePackDataUtilsHP.ProcessWeight();
                            var categories = CarePackDataUtilsHP.ProcessCategories();
                            var manageStock = true;

                            try
                            {
                                var strPrice = linha.Cell(14).Value.ToString();
                                double doublePrice = double.Parse(strPrice);
                                doublePrice = doublePrice / (1 - (MARGIN / 100));
                                price = doublePrice.ToString();
                                regularPrice = doublePrice.ToString();
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
                var caminhoArquivoJson = Path.Combine(Directory.GetCurrentDirectory(), "carepackHP.json");
                File.WriteAllText(caminhoArquivoJson, json);
                Console.WriteLine($"\n[INFO]: Arquivo carepackHP.json criado com sucesso em: {caminhoArquivoJson}");
                Console.WriteLine($"[INFO]: Tamanho do arquivo: {new FileInfo(caminhoArquivoJson).Length} bytes");
            }
            else
            {
                Console.WriteLine("[ERRO]: Nenhum produto foi processado.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO]: Erro ao processar arquivo de CarePack: {ex.Message}");
        }

        Console.WriteLine($"\n[INFO]: Processando arquivo de produtos: {caminhoArquivoProdutos}");
    }
}