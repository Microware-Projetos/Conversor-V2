using eCommerce.Shared.Models;
using ClosedXML.Excel;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ClosedXML.Excel;
using Newtonsoft.Json;
using System;

namespace eCommerce.Server.Processors.HP;

public static class HPProductProcessor
{
    
    public static void ProcessarListasProdutos(string caminhoArquivoProdutos, string caminhoArquivoPrecos)
    {
        try
        {
            var produtos = new List<WooProduct>();
            var listProdutos = new XLWorkbook(caminhoArquivoProdutos);
            var precosPorSku = GetPrecosPorSku(caminhoArquivoPrecos);
            
            foreach (var worksheet in listProdutos.Worksheets)
            {
                string aba = worksheet.Name;
                Console.WriteLine($"Processando aba: {aba}");
                
                if (aba == "Desktops")
                {
                    var linhas = worksheet.RowsUsed().Skip(2);
                    int contadorLinhas = 0;

                    foreach (var linha in linhas)
                    {
                        try
                        {

                            var sku = linha.Cell(2).Value.ToString() ?? "";
                            var preco = precosPorSku[sku];

                            if (preco == "0" || preco == "" || preco == null)
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

                            var produto = new WooProduct
                            {
                                name = "Desktop " + model,
                                sku = sku,
                                short_description = "HP " + model,
                                description = "Desktop " + model + " " + processor + " " + memory + " " + storage + " " + os,
                                price = preco.ToString(),
                                regular_price = preco.ToString(),
                                stock_quantity = "10",
                                weight = weight,
                                manage_stock = true,
                                shipping_class = "desktop",
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

    public static Dictionary<string, string> GetPrecosPorSku(string caminhoArquivoPrecos)
    {
        var precosPorSku = new Dictionary<string, string>();
        var listPrecos = new XLWorkbook(caminhoArquivoPrecos);

        foreach (var ws in listPrecos.Worksheets)
        {
            var linhasPrecos = ws.RowsUsed().Skip(1); // pula o cabeçalho

            // Pegando título das colunas que interessam uma única vez, no início:
            string tituloSku = ws.Cell(1, 2).GetString().Trim();     // coluna 2 = sku
            string tituloPreco = ws.Cell(1, 14).GetString().Trim();  // coluna 14 = preco (porque cell index começa em 1)
            Console.WriteLine($"Worksheet: {ws.Name} - Colunas: SKU='{tituloSku}', Preço='{tituloPreco}'");

            foreach (var linha in linhasPrecos)
            {
                var sku = linha.Cell(2).GetString().Trim();
                var preco = linha.Cell(14).GetString().Trim();

                Console.WriteLine($"SKU: {sku} - Preço: {preco}");

                if (!string.IsNullOrWhiteSpace(sku) && !precosPorSku.ContainsKey(sku))
                {
                    precosPorSku[sku] = preco;
                }
            }
        }

        return precosPorSku;
    }

  
}