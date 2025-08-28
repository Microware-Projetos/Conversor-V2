using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Newtonsoft.Json;
using eCommerce.Shared.Models;
using eCommerce.Server.Helpers;
using eCommerce.Server.Processors.Cisco;
using eCommerce.Server.Processors.Cisco.Helpers;
using WooAttribute = eCommerce.Shared.Models.Attribute;

namespace eCommerce.Server.Processors.Cisco;

public static class CiscoProductProcessor
{
    public static async Task ProcessarListasReal(string caminhoArquivoProdutos, CancellationToken cancellationToken = default)
    {
        const string CHACHE_DIR = "Cache/Cisco";
        const string STOCK_QUANTITY = "10";

        try
        {
            try
            {
                // Verificar cancelamento
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR]: Erro ao processar produtos: {ex.Message}");
                throw;
            }

            CacheManagerCisco.EnsureCacheDir(CHACHE_DIR);

            var products = new List<WooProduct>();
            var listProducts = new XLWorkbook(caminhoArquivoProdutos);
            var sheet = listProducts.Worksheet(1);
            var linhas = sheet.RowsUsed().Skip(1);
            var cabecalho = sheet.Row(1);
            var contadorLinhas = 0;
            
            foreach (var linha in linhas)
            {
                try
                {
                    var sku = linha.Cell(4).Value.ToString().Trim() ?? string.Empty;
                    sku = ProductDataUtilsCisco.ClearSku(sku);
                    var nome = linha.Cell(8).Value.ToString().Trim() ?? string.Empty;
                    nome = nome.Replace("FAST TRACK__","");

                    var ncm =linha.Cell(5).Value.ToString().Trim() ?? string.Empty;

                    if ((string.IsNullOrEmpty(sku) || string.IsNullOrEmpty(ncm)) || (sku == "nan" || nome == "nan"))
                    {
                        continue;
                    }
                    
                    var strigPrice = linha.Cell(10).Value.ToString().Trim() ?? string.Empty;
                    var price = ProductDataUtilsCisco.GetValueStringToDouble(strigPrice);
                    var sellingPrice = price / (1 - (20 / 100));

                    var icms = linha.Cell(17).Value.ToString().Trim() ?? "0";
                    var leadtime = ProductDataUtilsCisco.GetLeadtime(icms);

                    var produto = new WooProduct
                    {
                        name = nome,
                        sku = sku,
                        short_description = nome,
                        description = nome,
                        price = sellingPrice.ToString(),
                        regular_price = sellingPrice.ToString(),
                        stock_quantity = STOCK_QUANTITY,
                        manage_stock = true,
                        meta_data = ProductDataUtilsCisco.GetPhotos(), //PENDENTE | NO PYTHON PASSA O PARAMETRO LINHA
                        categories = ProductDataUtilsCisco.GetCategories(ncm), //PENDENTE | NO PYTHON PASSA O PARAMETRO LINHA
                        shipping_class = leadtime,
                        attributes = ProductDataUtilsCisco.GetAttributes(ncm, leadtime) //PENDENTE | NO PYTHON PASSA O PARAMETRO LINHA
                    };

                    products.Add(produto);
                    contadorLinhas++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR]: Erro ao processar produtos: {ex.Message}");
                }
            }

            Console.WriteLine($"[INFO]: Processadas {contadorLinhas} linhas");
            Console.WriteLine($"[INFO]: Total de produtos: {products.Count}");

            if (products.Count > 0)
            {
                //Salvar produtos em um json;
                var json = JsonConvert.SerializeObject(products, Formatting.Indented, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    NullValueHandling = NullValueHandling.Ignore,
                    ObjectCreationHandling = ObjectCreationHandling.Replace
                });
                var caminhoArquivoJson = Path.Combine(Directory.GetCurrentDirectory(), "produtosDolarCisco.json");
                File.WriteAllText(caminhoArquivoJson, json);
                Console.WriteLine($"[INFO]: Arquivo produtosDolarCisco.json criado com sucesso em: {caminhoArquivoJson}");
                Console.WriteLine($"[INFO]: Tamanho do arquivo: {new FileInfo(caminhoArquivoJson).Length} bytes");
            }
            else
            {
                Console.WriteLine("[INFO]: Nenhum produto foi processado.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR]: Erro ao processar produtos: {ex.Message}");
            Console.WriteLine($"[ERROR]: Stack trace: {ex.StackTrace}");
        }
    }
    
    public static async Task ProcessarListasDolar(string caminhoArquivoProdutos, double dolar, CancellationToken cancellationToken = default)
    {
        const string CHACHE_DIR = "Cache/Cisco";
        const string STOCK_QUANTITY = "10";

        try
        {

            try
            {
                // Verificar cancelamento
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR]: Erro ao processar produtos: {ex.Message}");
                throw;
            }

            CacheManagerCisco.EnsureCacheDir(CHACHE_DIR);

            var products = new List<WooProduct>();
            var listProducts = new XLWorkbook(caminhoArquivoProdutos);
            var sheet = listProducts.Worksheet(1);
            var linhas = sheet.RowsUsed().Skip(1);
            var cabecalho = sheet.Row(1);
            var contadorLinhas = 0;
            
            foreach (var linha in linhas)
            {
                try
                {
                    var sku = linha.Cell(4).Value.ToString().Trim() ?? string.Empty;
                    sku = ProductDataUtilsCisco.ClearSku(sku);
                    var nome = linha.Cell(8).Value.ToString().Trim() ?? string.Empty;
                    nome = nome.Replace("FAST TRACK__","");

                    var ncm =linha.Cell(5).Value.ToString().Trim() ?? string.Empty;

                    if ((string.IsNullOrEmpty(sku) || string.IsNullOrEmpty(ncm)) || (sku == "nan" || nome == "nan"))
                    {
                        continue;
                    }
                    
                    var strigPrice = linha.Cell(10).Value.ToString().Trim() ?? string.Empty;
                    var price = ProductDataUtilsCisco.GetValueStringToDouble(strigPrice);
                    var priceDolar = price * dolar;
                    var sellingPrice = priceDolar / (1 - (20 / 100));

                    var icms = linha.Cell(17).Value.ToString().Trim() ?? "0";
                    var leadtime = ProductDataUtilsCisco.GetLeadtime(icms);

                    var produto = new WooProduct
                    {
                        name = nome,
                        sku = sku,
                        short_description = nome,
                        description = nome,
                        price = sellingPrice.ToString(),
                        regular_price = sellingPrice.ToString(),
                        stock_quantity = STOCK_QUANTITY,
                        manage_stock = true,
                        meta_data = ProductDataUtilsCisco.GetPhotos(), //PENDENTE | NO PYTHON PASSA O PARAMETRO LINHA
                        categories = ProductDataUtilsCisco.GetCategories(ncm), //PENDENTE | NO PYTHON PASSA O PARAMETRO LINHA
                        shipping_class = leadtime,
                        attributes = ProductDataUtilsCisco.GetAttributes(ncm, leadtime) //PENDENTE | NO PYTHON PASSA O PARAMETRO LINHA
                    };

                    products.Add(produto);
                    contadorLinhas++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR]: Erro ao processar produtos: {ex.Message}");

                }
            }

            Console.WriteLine($"[INFO]: Processadas {contadorLinhas} linhas");
            Console.WriteLine($"[INFO]: Total de produtos: {products.Count}");

            if (products.Count > 0)
            {
                //Salvar produtos em um json;
                var json = JsonConvert.SerializeObject(products, Formatting.Indented, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    NullValueHandling = NullValueHandling.Ignore,
                    ObjectCreationHandling = ObjectCreationHandling.Replace
                });
                var caminhoArquivoJson = Path.Combine(Directory.GetCurrentDirectory(), "produtosDolarCisco.json");
                File.WriteAllText(caminhoArquivoJson, json);
                Console.WriteLine($"[INFO]: Arquivo produtosDolarCisco.json criado com sucesso em: {caminhoArquivoJson}");
                Console.WriteLine($"[INFO]: Tamanho do arquivo: {new FileInfo(caminhoArquivoJson).Length} bytes");
            }
            else
            {
                Console.WriteLine("[INFO]: Nenhum produto foi processado.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR]: Erro ao processar produtos: {ex.Message}");
            Console.WriteLine($"[ERROR]: Stack trace: {ex.StackTrace}");
        }
    }
}