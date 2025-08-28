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

public static class HPWordPressPlotter
{
    private const string WOOCOMMERCE_BASE_URL = "https://ecommerce.microware.com.br/hp/wp-json/wc/v3/products";
    private const string WOOCOMMERCE_CONSUMER_KEY = "ck_3e315f613482b1c092d5304ac8cd95e4c28806d1";
    private const string WOOCOMMERCE_CONSUMER_SECRET = "cs_7ab0dd32ba84a9ccfe28a53ba3ad1daa94415488";

    private static readonly HttpClient _http = new HttpClient();

    public static async Task EnviarListaDePlotter(List<WooProduct> plotters)
    {
        var byteArray = Encoding.ASCII.GetBytes($"{WOOCOMMERCE_CONSUMER_KEY}:{WOOCOMMERCE_CONSUMER_SECRET}");
        var authHeader = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

        _http.DefaultRequestHeaders.Authorization = authHeader;

        try
        {
            Console.WriteLine("[INFO]: Buscando todos as plotters...");
            var todasPlotters = await BuscarPlotters();

            //TESTES ABAIXO APÓS RECEBIMENTO DA NOVA TABELA
            /*
            Console.WriteLine("[INFO]: Atualizando estoques para 0...");
            await MudarEstoqueParaZero(todasPlotters);

            Console.WriteLine("[INFO]: Deletando todos as plotters...");
            await DeletarTodasPlotters(plotters, todasPlotters);

            Console.WriteLine("[INFO]: Enviando plotters para o WooCommerce...");
            await EnviarPlottersEmLotes(plotters);
            */

        }
        catch(Exception ex)
        {
            Console.WriteLine($"[ERROR]: Erro ao enviar lista de plotter: {ex.Message}");
            return;
        }
    }

    private static async Task<List<WooProduct>> BuscarPlotters()
    {
        Console.WriteLine($"[INFO]: Buscando plotters no WooCommerce...");
        var todasPlotters = new List<WooProduct>();

        int page = 1;

        while(true)
        {
            Console.WriteLine($"[INFO]: Buscando plotters no WooCommerce... Pagina: {page}");

            var response = await _http.GetAsync($"{WOOCOMMERCE_BASE_URL}?per_page=100&page={page}");
            if(!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[ERROR]: Erro ao buscar plotter: {response.StatusCode}");
                break;
            }

            Console.WriteLine($"[INFO]: Get response do WooCommerce com sucesso.");
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[INFO]: Content do WooCommerce: {!(string.IsNullOrEmpty(content))}");
            Console.WriteLine($"[INFO]: Transformando content em lista de plotters...");
            var plotters = JsonConvert.DeserializeObject<List<WooProduct>>(content);
            Console.WriteLine($"[INFO]: Plotters encontrados: {plotters.Count}");

            if(plotters == null || plotters.Count == 0)
            {
                Console.WriteLine($"[INFO]: Nenhum plotter encontrado na pagina {page}");
                break;
            }

            Console.WriteLine($"[INFO]: Iniciando processo de filtragem de plotters...");
            foreach(var plotter in plotters)
            {
                var categories = plotter.categories.FirstOrDefault()?.id;
                var sku = plotter.sku;

                if(categories == 24)
                {
                    Console.WriteLine($"[INFO]: ID da categoria da plotter: {categories}");
                    Console.WriteLine($"[INFO]: SKU da plotter: {sku}");
                    Console.WriteLine($"[INFO]: Plotter adicionado: {sku}");
                    todasPlotters.Add(plotter);
                }
            }
            Console.WriteLine($"[INFO]: Indo para a proxima pagina...");
            page++;
        }

        if(!todasPlotters.Any())
        {
            Console.WriteLine("[INFO]: Nenhum plotter encontrado.");
            return null;
        }

        Console.WriteLine($"[INFO]: Encontrados {todasPlotters.Count} plotters.");

        return todasPlotters;
    }

    private static async Task MudarEstoqueParaZero(List<WooProduct> plotters)
    {
        Console.WriteLine($"[INFO]: Atualizando estoque para 0...");
        foreach(var plotter in plotters)
        {
            plotter.stock_quantity = "0";
            await AtualizarPlotter(plotter);
        }
    }

    private static async Task AtualizarPlotter(WooProduct plotter)
    {
        var json = JsonConvert.SerializeObject(plotter);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PutAsync($"{WOOCOMMERCE_BASE_URL}/{plotter.id}", content);

        if(response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[INFO]: Plotter {plotter.id} atualizado com sucesso.");
        }
        else
        {
            var erro = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[ERROR]: Erro ao atualizar plotter {plotter.id}: {erro}");
        }
    }

    private static async Task DeletarTodasPlotters(List<WooProduct> plotters, List<WooProduct> todasPlotters)
    {            
        Console.WriteLine($"[INFO]: Deletando todos as plotters...");
        var todosIds = new List<int>();

        foreach(var plotter in todasPlotters)
        {
            if(plotters.Any(p => p.sku == plotter.sku))
            {
                todosIds.Add(plotter.id ?? 0);
            }
        }

        for (int i = 0; i<todosIds.Count; i += 5)
        {
            var grupoIds = todosIds.Skip(i).Take(5);

            var tarefas = grupoIds.Select(id => DeletarProduto(id)).ToArray();
            await Task.WhenAll(tarefas);

            Console.WriteLine($"[INFO]: Deletados {grupoIds.Count()} plotters.");
            Thread.Sleep(1000);
        }
    }

    private static async Task DeletarProduto(int id)
    {
        var response = await _http.DeleteAsync($"{WOOCOMMERCE_BASE_URL}/{id}?force=true");

        if(response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[INFO]: Plotter {id} deletado com sucesso.");
        }
        else
        {
            var erro = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[ERROR]: Erro ao deletar plotter {id}: {erro}");
        }
    }

    private static async Task EnviarPlottersEmLotes(List<WooProduct> plotters)
    {
        Console.WriteLine($"[INFO]: Enviando plotters em lotes...");
        var lotes = new List<List<WooProduct>>();

        for(int i = 0; i < plotters.Count; i += 10)
        {
            lotes.Add(plotters.Skip(i).Take(10).ToList());
            Console.WriteLine($"[INFO]: Lote {i} de {plotters.Count} criado.");
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

        Console.WriteLine("[INFO]: Todas as plotters foram processadas com sucesso.");
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