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
using eCommerce.Server.Processors.Lenovo;
using eCommerce.Server.Processors.Lenovo.Helpers;
using eCommerce.Server.Wordpress.Lenovo;

namespace eCommerce.Server.Processors.Lenovo;

public static class LenovoCarePackProcessor
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

                if (aba == "T1")
                {
                    var linhas = worksheet.RowsUsed().Skip(4);
                    var cabecalho = worksheet.Row(6);
                    int contadorLinhas = 0;

                    foreach (var linha in linhas)
                    {
                        // Verificar cancelamento
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            var sku = linha.Cell(1).Value.ToString() ?? "";
                            var productName = linha.Cell(4).Value.ToString() ?? "";
                            var shortDescription = linha.Cell(4).Value.ToString() ?? "";
                            var description = linha.Cell(4).Value.ToString() ?? "";
                            var price = "";
                            var regularPrice = "";
                            var stockQuantity = STOCK.ToString();
                            var attributes = CarePackDataUtilsLenovo.ProcessAttributes(linha); 
                            var metaData = CarePackDataUtilsLenovo.ProcessPhotos(); 
                            var dimensions = CarePackDataUtilsLenovo.ProcessDimensions(); 
                            var weight = CarePackDataUtilsLenovo.ProcessWeight(); 
                            var categories = CarePackDataUtilsLenovo.ProcessCategories();
                            var manageStock = true;

                            try
                            {
                                var strPrice = linha.Cell(12).Value.ToString();
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
                var caminhoArquivoJson = Path.Combine(Directory.GetCurrentDirectory(), "carepackLenovo.json");
                File.WriteAllText(caminhoArquivoJson, json);
                Console.WriteLine($"\n[INFO]: Arquivo carepackLenovo.json criado com sucesso em: {caminhoArquivoJson}");
                Console.WriteLine($"[INFO]: Tamanho do arquivo: {new FileInfo(caminhoArquivoJson).Length} bytes");

                //Enviar produtos para a API
                Console.WriteLine($"[INFO]: Enviando carepack para a API, WooCommerce...");
                await LenovoWordPressCarePack.EnviarListaDeCarePack(produtos);
                Console.WriteLine($"[INFO]: Carepack enviado para a API com sucesso.");
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