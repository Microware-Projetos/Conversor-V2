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
using eCommerce.Server.Services.HP;
using eCommerce.Server.Wordpress.HP;

namespace eCommerce.Server.Processors.HP;

public static class HPPromocaoProcessor
{

    public static async Task ProcessarListasPromocao(string caminhoArquivoProdutos, CancellationToken cancellationToken = default)
    {
        try
        {
            Console.WriteLine($"[INFO]: Iniciando processamento de listas de promoção...");
            // Verificar cancelamento
            cancellationToken.ThrowIfCancellationRequested();
            
            // Verifica se os arquivos existem
            if (!File.Exists(caminhoArquivoProdutos))
            {
                throw new FileNotFoundException($"[ERROR]: Arquivo de produtos não encontrado: {caminhoArquivoProdutos}");
            }
            
            Console.WriteLine($"[INFO]: Arquivo de produtos: {caminhoArquivoProdutos}");
            
            var produtos = new List<WooProduct>();
            var produtosNaoEncontrados = new List<string>();

            var listProdutos = new XLWorkbook(caminhoArquivoProdutos);
            
            foreach (var worksheet in listProdutos.Worksheets)
            {
                // Verificar cancelamento a cada aba
                cancellationToken.ThrowIfCancellationRequested();
                
                string aba = worksheet.Name;
                Console.WriteLine($"Processando aba: {aba}");
                
                if (aba == "SP")
                {
                    var linhas = worksheet.RowsUsed().Skip(1);
                    var cabecalho = worksheet.Row(1);
                    int contadorLinhas = 0;

                    foreach (var linha in linhas)
                    {
                        // Verificar cancelamento a cada linha
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        try
                        {
                            var sku = linha.Cell(1).Value.ToString().Trim() ?? "";
                            
                            if (sku == "" || sku == null || sku == "-")
                            {
                                continue;
                            }

                            var produto = await HPWordPressPromocao.BuscarProduto(sku);

                            if(produto != null)
                            {
                                Console.WriteLine($"[INFO]: Produto {produto.name} encontrado no WooCommerce.");
                                Console.WriteLine($"[INFO]: Adicionando produto na lista de produtos...");
                                produtos.Add(produto);

                                var produtoId = produto.id ?? 0;
                                Console.WriteLine($"[INFO]: Produto ID coletado: {produtoId}");
                                
                                if(produtoId == 0)
                                {
                                    Console.WriteLine($"[ERROR]: Produto ID não encontrado para o SKU: {sku}");
                                    continue;
                                }

                                var salePriceString = linha.Cell(10).Value.ToString().Trim() ?? "";
                                Console.WriteLine($"[INFO]: Preço de venda coletado: {salePriceString}");
                                double salePrice = 0;

                                try
                                {
                                    salePrice = double.Parse(salePriceString) / (1 - (20 / 100));
                                    Console.WriteLine($"[INFO]: Preço de venda: {salePrice}");
                                }
                                catch(Exception ex)
                                {
                                    Console.WriteLine($"[ERROR]: Erro ao converter preço de venda: {ex.Message}");
                                }

                                Console.WriteLine($"[INFO]: Preço de venda do produto {sku}: {salePrice}");

                                Dictionary<string, string> data = new Dictionary<string, string>
                                {
                                    {"sale_price", salePrice.ToString()},
                                };

                                await HPWordPressPromocao.AtualizarProduto(produtoId, data);
                            }
                            else
                            {
                                Console.WriteLine($"[INFO]: Produto {sku} não encontrado no WooCommerce.");
                                produtosNaoEncontrados.Add(sku);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR]: Erro ao processar linha: {ex.Message}");
                        }

                        contadorLinhas++;
                    }

                    Console.WriteLine($"[INFO]: Processadas {contadorLinhas} linhas na aba SP");
                }
            }

            Console.WriteLine($"[INFO]: Total de produtos processados: {produtos.Count}");
            
            if (produtos.Count > 0)
            {
                Console.WriteLine($"[INFO]: Produtos processados:\n{string.Join("\n- ", produtos.Select(p => p.name))}");
            }
            else
            {
                Console.WriteLine("[INFO]: Nenhum produto foi processado.");
            }

            if(produtosNaoEncontrados.Count > 0)
            {
                Console.WriteLine($"[INFO]: Produtos não encontrados:\n{string.Join("\n- ", produtosNaoEncontrados)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR]: Erro durante o processamento: {ex.Message}");
            Console.WriteLine($"[ERROR]: Stack trace: {ex.StackTrace}");
        }
    }
}