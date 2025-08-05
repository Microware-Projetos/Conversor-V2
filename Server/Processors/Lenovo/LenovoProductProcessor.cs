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

namespace eCommerce.Server.Processors.Lenovo;

public static class LenovoProductProcessor
{
    public static async Task ProcessarListasProdutos(string caminhoArquivoProdutos, CancellationToken cancellationToken = default)
    {

        const int MARGIN = 20;
        const int STOCK = 10;
        const string CHACHE_DIR = "Cache/Lenovo";

        try

        {
            // Verificar cancelamento
            cancellationToken.ThrowIfCancellationRequested();

            // Verifica se o arquivo existe
            if (!File.Exists(caminhoArquivoProdutos))
            {
                throw new FileNotFoundException($"Arquivo de produtos não encontrado: {caminhoArquivoProdutos}");
            }

            Console.WriteLine($"Arquivo de produtos: {caminhoArquivoProdutos}");

            // Garante que o diretório de cache existe
            CacheManagerLenovo.EnsureCacheDir(CHACHE_DIR);

            //TODO: Implementar NormalizeUtisLenovo BuscarImagens
            var images = await NormalizeUtisLenovo.BuscarImagens();

            var deliveryInfo = await NormalizeUtisLenovo.BuscarDelivery();
            
            // Criar um dicionário vazio para deliveryInfo
            var deliveryInfoDict = new Dictionary<string, string>();
            
            // Normalizar valores
            var normalizedFamily = NormalizeUtisLenovo.NormalizeValuesList("Familia");
            var normalizedAnatel = NormalizeUtisLenovo.NormalizeValuesList("Anatel");

            var produtos = new List<WooProduct>();
            var listProdutos = new XLWorkbook(caminhoArquivoProdutos);

            foreach (var worksheet in listProdutos.Worksheets)
            {
                // Verificar cancelamento a cada aba
                cancellationToken.ThrowIfCancellationRequested();

                string aba = worksheet.Name;
                Console.WriteLine($"Processando aba: {aba}");

                if (aba == "Notebook")
                {
                    var linhas = worksheet.RowsUsed().Skip(1);
                    var cabecalho = worksheet.Row(1);
                    int contadorLinhas = 0;

                    foreach (var linha in linhas)
                    {
                        // Verificar cancelamento a cada linha
                        cancellationToken.ThrowIfCancellationRequested();
                        if (linha.Cell(1).Value.ToString().Contains("Revenda sem Regime") && linha.Cell(2).Value.ToString().Contains("SP"))
                        {
                            try
                            {
                                var sku = linha.Cell(3).Value.ToString() ?? "";
                                var shortDescription = linha.Cell(5).Value.ToString() ?? "";
                                var model = linha.Cell(5).Value.ToString() ?? "";
                                var productName = linha.Cell(6).Value.ToString() ?? "";
                                var description = linha.Cell(6).Value.ToString() ?? "";
                                var price = "";
                                var regularPrice = "";
                                var stockQuantity = STOCK;
                                var manageStock = true;
                                var shippingClass = "importado";
                                var processor = linha.Cell(9).Value.ToString() ?? "";
                                var graphics = linha.Cell(11).Value.ToString() ?? "";
                                var chipset = linha.Cell(12).Value.ToString() ?? "";
                                var display = linha.Cell(17).Value.ToString() ?? "";
                                var touchScreen = linha.Cell(18).Value.ToString() ?? "";
                                var ethernet = linha.Cell(22).Value.ToString() ?? "";
                                var wLanBluetooth = linha.Cell(23).Value.ToString() ?? "";
                                var caseMaterial = linha.Cell(24).Value.ToString() ?? "";
                                var camera = linha.Cell(25).Value.ToString() ?? "";
                                var microphone = linha.Cell(26).Value.ToString() ?? "";
                                var color = linha.Cell(27).Value.ToString() ?? "";
                                var keyboard = linha.Cell(28).Value.ToString() ?? "";
                                var fingerPrintReader = linha.Cell(29).Value.ToString() ?? "";
                                var battery = linha.Cell(31).Value.ToString() ?? "";
                                var os = linha.Cell(33).Value.ToString() ?? "";

                                //TODO: Implementar GetDimensions
                                var dimensions = linha.Cell(34).Value.ToString() ?? "";
                                var weight = linha.Cell(35).Value.ToString() ?? "";
                                var ean = linha.Cell(36).Value.ToString() ?? "";

                                var prodInfo = new Dictionary<string, string>
                                {
                                    { "EAN", ean },
                                    { "sku", sku },
                                    { "model", model }
                                };
                                var attributes = AttributeProcessorLenovo.ProcessarAttributes(sku, model, linha, cabecalho, prodInfo, aba, normalizedAnatel, normalizedFamily);

                                // Verifica se o preço vindo da planilha é válido e converte para o formato de preço do WooCommerce
                                try
                                {
                                    var strPrice = linha.Cell(41).GetString().Trim();
                                    double doublePrice = double.Parse(strPrice);
                                    doublePrice = doublePrice / (1 - (MARGIN / 100));
                                    price = doublePrice.ToString();
                                    regularPrice = doublePrice.ToString();
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Erro ao converter preço: {ex.Message}");
                                    continue;
                                }

                                Console.WriteLine($"Valor de SKU enviado ao GetProductBySKU: {sku}");
                                var productData = await DataProcessorLenovo.GetProductBySKU(sku);

                                // Verifica se o início do nome do produto é NB e substitui por Notebook
                                if (productName.StartsWith("NB"))
                                    productName = productName.Replace("NB", "Notebook");

                                // Verifica se o campo PART_ORIGIN é IMPORTED e se não for, define como local
                                if (linha.Cell(7).Value.ToString() != "IMPORTED")
                                    shippingClass = "local";

                                var produto = new WooProduct
                                {
                                    name = productName,
                                    sku = sku,
                                    short_description = shortDescription,
                                    description = description,
                                    price = price,
                                    regular_price = regularPrice,
                                    stock_quantity = stockQuantity.ToString(),
                                    weight = DataUtilsLenovo.ProcessWeight(linha, deliveryInfoDict, productData),
                                    manage_stock = manageStock,
                                    shipping_class = shippingClass,
                                    attributes = attributes,
                                    dimensions = DataUtilsLenovo.ProcessDimensions(linha, deliveryInfoDict, productData),
                                    categories = DataUtilsLenovo.ProcessCategories(linha),
                                    meta_data = await DataUtilsLenovo.ProcessPhotos(linha, images, normalizedFamily, productData)
                                };
                                
                                produtos.Add(produto);
                                contadorLinhas++;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Erro ao processar linha: {ex.Message}");
                                continue;
                            }
                        }
                    }
                }
                else
                    Console.WriteLine($"Aba não processada: {aba}");
            }

            if (produtos.Count > 0)
            {
            //Salvar produtos em um json;
            var json = JsonConvert.SerializeObject(produtos, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                NullValueHandling = NullValueHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Replace
            });
            var caminhoArquivoJson = Path.Combine(Directory.GetCurrentDirectory(), "produtosLenovo.json");
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
            Console.WriteLine($"Erro ao processar arquivo de produtos: {ex.Message}");
        }

        Console.WriteLine($"Processando arquivo de produtos: {caminhoArquivoProdutos}");
    }
}