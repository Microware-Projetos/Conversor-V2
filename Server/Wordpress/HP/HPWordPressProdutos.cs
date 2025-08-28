using eCommerce.Shared.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Linq;
using System.IO;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;
using WooProduct = eCommerce.Shared.Models.WooProduct;

namespace eCommerce.Server.Wordpress.HP;

public static class HPWordPressProdutos
{
    private const string WOOCOMMERCE_BASE_URL = "https://ecommerce.microware.com.br/hp/wp-json/wc/v3/products";
    private const string WOOCOMMERCE_CONSUMER_KEY = "ck_3e315f613482b1c092d5304ac8cd95e4c28806d1";
    private const string WOOCOMMERCE_CONSUMER_SECRET = "cs_7ab0dd32ba84a9ccfe28a53ba3ad1daa94415488";

    private static readonly HttpClient _http = new HttpClient();

    public static async Task EnviarListaDeProdutos(List<WooProduct> produtos)
    {
        var byteArray = Encoding.ASCII.GetBytes($"{WOOCOMMERCE_CONSUMER_KEY}:{WOOCOMMERCE_CONSUMER_SECRET}");
        var authHeader = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

        _http.DefaultRequestHeaders.Authorization = authHeader;

        try
        {
            Console.WriteLine("[INFO]: Buscando todos os produtos...");
            var todosProdutos = await BuscarProdutos();

            //TESTES ABAIXO APÓS RECEBIMENTO DA NOVA TABELA
            /*
            Console.WriteLine("[INFO]: Atualizando estoques para 0...");
            await MudarEstoqueParaZero(todosProdutos);

            Console.WriteLine("[INFO]: Deletando todos os produtos...");
            await DeletarTodosProdutos(produtos, todosProdutos);

            Console.WriteLine("[INFO]: Enviando produtos para o WooCommerce...");
            await EnviarProdutosEmLotes(produtos);
            */

        }
        catch(Exception ex)
        {
            Console.WriteLine($"[ERROR]: Erro ao enviar lista de produtos: {ex.Message}");
            return;
        }
    }

    private static async Task<List<WooProduct>> BuscarProdutos()
    {
        Console.WriteLine($"[INFO]: Buscando produtos no WooCommerce...");
        var todosProdutos = new List<WooProduct>();

        int page = 1;

        while(true)
        {
            Console.WriteLine($"[INFO]: Buscando produtos no WooCommerce... Pagina: {page}");

            var response = await _http.GetAsync($"{WOOCOMMERCE_BASE_URL}?per_page=100&page={page}");
            if(!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[ERROR]: Erro ao buscar produtos: {response.StatusCode}");
                break;
            }

            Console.WriteLine($"[INFO]: Get response do WooCommerce com sucesso.");
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[INFO]: Content do WooCommerce: {!(string.IsNullOrEmpty(content))}");
            Console.WriteLine($"[INFO]: Transformando content em lista de produtos...");
            var products = JsonConvert.DeserializeObject<List<WooProduct>>(content);
            Console.WriteLine($"[INFO]: Produtos encontrados: {products.Count}");

            if(products == null || products.Count == 0)
            {
                Console.WriteLine($"[INFO]: Nenhum produto encontrado na pagina {page}");
                break;
            }

            Console.WriteLine($"[INFO]: Iniciando processo de filtragem de produtos...");
            foreach(var product in products)
            {
                var categories = product.categories.FirstOrDefault()?.id;
                var sku = product.sku;

                if(categories != 24 && categories != 32)
                {
                    Console.WriteLine($"[INFO]: ID da categoria do produto: {categories}");
                    Console.WriteLine($"[INFO]: SKU do produto: {sku}");
                    Console.WriteLine($"[INFO]: Produto adicionado: {sku}");
                    todosProdutos.Add(product);
                }
            }
            Console.WriteLine($"[INFO]: Indo para a proxima pagina...");
            page++;
        }

        if(!todosProdutos.Any())
        {
            Console.WriteLine("[INFO]: Nenhum produto encontrado.");
            return null;
        }

        Console.WriteLine($"[INFO]: Encontrados {todosProdutos.Count} produtos.");

        return todosProdutos;
    }

    private static async Task MudarEstoqueParaZero(List<WooProduct> produtos)
    {
        Console.WriteLine($"[INFO]: Atualizando estoque para 0...");
        foreach(var product in produtos)
        {
            product.stock_quantity = "0";
            await AtualizarProduto(product);
        }
    }

    private static async Task AtualizarProduto(WooProduct product)
    {
        var json = JsonConvert.SerializeObject(product);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PutAsync($"{WOOCOMMERCE_BASE_URL}/{product.id}", content);

        if(response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[INFO]: Produto {product.id} atualizado com sucesso.");
        }
        else
        {
            var erro = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[ERROR]: Erro ao atualizar produto {product.id}: {erro}");
        }
    }

    private static async Task DeletarTodosProdutos(List<WooProduct> produtos, List<WooProduct> todosProdutos)
    {            
        Console.WriteLine($"[INFO]: Deletando todos os produtos...");
        var todosIds = new List<int>();

        foreach(var product in todosProdutos)
        {
            if(produtos.Any(p => p.sku == product.sku))
            {
                todosIds.Add(product.id ?? 0);
            }
        }

        for (int i = 0; i<todosIds.Count; i += 5)
        {
            var grupoIds = todosIds.Skip(i).Take(5);

            var tarefas = grupoIds.Select(id => DeletarProduto(id)).ToArray();
            await Task.WhenAll(tarefas);

            Console.WriteLine($"[INFO]: Deletados {grupoIds.Count()} produtos.");
            Thread.Sleep(1000);
        }
    }

    private static async Task DeletarProduto(int id)
    {
        var response = await _http.DeleteAsync($"{WOOCOMMERCE_BASE_URL}/{id}?force=true");

        if(response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[INFO]: Produto {id} deletado com sucesso.");
        }
        else
        {
            var erro = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[ERROR]: Erro ao deletar produto {id}: {erro}");
        }
    }

    private static async Task EnviarProdutosEmLotes(List<WooProduct> produtos)
    {
        Console.WriteLine($"[INFO]: Enviando produtos em lotes...");
        var lotes = new List<List<WooProduct>>();

        for(int i = 0; i < produtos.Count; i += 10)
        {
            lotes.Add(produtos.Skip(i).Take(10).ToList());
            Console.WriteLine($"[INFO]: Lote {i} de {produtos.Count} criado.");
        }

        var tarefas = new List<Task>();

        int loteAtual = 1;
        foreach(var lote in lotes)
        {
            Console.WriteLine($"[INFO]: Lote {loteAtual} de {lotes.Count}");
            var tarefa = EnviarLote(lote, loteAtual);
            tarefas.Add(tarefa);
            loteAtual++;
        }

        await Task.WhenAll(tarefas);

        Console.WriteLine("[INFO]: Todos os produtos foram processados com sucesso.");
    }

    private static async Task EnviarLote(List<WooProduct> lote, int loteAtual, int maxTentativas = 3)
    {
        Console.WriteLine($"[INFO]: Enviando lote {loteAtual}...");
        var url = $"{WOOCOMMERCE_BASE_URL}/batch";

        for(int tentativa = 1; tentativa <= maxTentativas; tentativa++)
        {
            try
            {
                var payload = new
                {
                    create = lote
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _http.PostAsync(url, content);

                if(response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[INFO]: Lote {loteAtual} enviado com sucesso na tentativa {tentativa}.");
                    return;
                }
                else
                {
                    Console.WriteLine($"[ERROR]: Erro ao enviar lote {loteAtual}, Status Code: {response.StatusCode}");
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"[ERROR]: Erro ao enviar lote {loteAtual} na tentativa {tentativa}: {ex.Message}");
            }

            if (tentativa < maxTentativas)
            {
                Console.WriteLine($"[INFO]: Reprocessando lote {loteAtual} (tentativa {tentativa + 1} de {maxTentativas})");
                await Task.Delay(2000);
            }
        }

        Console.WriteLine($"[ERROR]: Lote {loteAtual} falhou após {maxTentativas} tentativas.");

        File.WriteAllText($"lote_falha_{loteAtual}.json",
            JsonConvert.SerializeObject(lote, Formatting.Indented));
        
        return;
    }
}