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

public static class HPWordPressCarePack
{
    private const string WOOCOMMERCE_BASE_URL = "https://ecommerce.microware.com.br/hp/wp-json/wc/v3/products";
    private const string WOOCOMMERCE_CONSUMER_KEY = "ck_3e315f613482b1c092d5304ac8cd95e4c28806d1";
    private const string WOOCOMMERCE_CONSUMER_SECRET = "cs_7ab0dd32ba84a9ccfe28a53ba3ad1daa94415488";

    private static readonly HttpClient _http = new HttpClient();

    public static async Task EnviarListaDeCarePack(List<WooProduct> carePacks)
    {
        var byteArray = Encoding.ASCII.GetBytes($"{WOOCOMMERCE_CONSUMER_KEY}:{WOOCOMMERCE_CONSUMER_SECRET}");
        var authHeader = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

        _http.DefaultRequestHeaders.Authorization = authHeader;

        try
        {
            Console.WriteLine("[INFO]: Buscando todos os carepacks...");
            var todosCarePacks = await BuscarCarePacks();

            //TESTES ABAIXO APÓS RECEBIMENTO DA NOVA TABELA
            /*
            Console.WriteLine("[INFO]: Deletando todos os carepacks...");
            await DeletarTodosCarePacks(carePacks, todosCarePacks);

            Console.WriteLine("[INFO]: Enviando carepacks para o WooCommerce...");
            await EnviarCarePacksEmLotes(carePacks);
            */

        }
        catch(Exception ex)
        {
            Console.WriteLine($"[ERROR]: Erro ao enviar lista de carepacks: {ex.Message}");
            return;
        }
    }

    private static async Task<List<WooProduct>> BuscarCarePacks()
    {
        Console.WriteLine($"[INFO]: Buscando carepacks no WooCommerce...");
        var todosCarePacks = new List<WooProduct>();

        int page = 1;

        while(true)
        {
            Console.WriteLine($"[INFO]: Buscando carepacks no WooCommerce... Pagina: {page}");

            var response = await _http.GetAsync($"{WOOCOMMERCE_BASE_URL}?per_page=100&page={page}&category=32");
            if(!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[ERROR]: Erro ao buscar carepacks: {response.StatusCode}");
                break;
            }

            Console.WriteLine($"[INFO]: Get response do WooCommerce com sucesso.");
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[INFO]: Content do WooCommerce: {!(string.IsNullOrEmpty(content))}");
            Console.WriteLine($"[INFO]: Transformando content em lista de carepacks...");
            var carePacks = JsonConvert.DeserializeObject<List<WooProduct>>(content);
            Console.WriteLine($"[INFO]: Carepacks encontrados: {carePacks.Count}");

            if(carePacks == null || carePacks.Count == 0)
            {
                Console.WriteLine($"[INFO]: Nenhum carepack encontrado na pagina {page}");
                break;
            }

            Console.WriteLine($"[INFO]: Iniciando processo de filtragem de carepacks...");
            foreach(var carePack in carePacks)
            {
                var categories = carePack.categories.FirstOrDefault()?.id;
                var sku = carePack.sku;

                if(categories == 32)
                {
                    Console.WriteLine($"[INFO]: ID da categoria do produto: {categories}");
                    Console.WriteLine($"[INFO]: SKU do produto: {sku}");
                    Console.WriteLine($"[INFO]: Carepack adicionado: {sku}");
                    todosCarePacks.Add(carePack);
                }
            }
            Console.WriteLine($"[INFO]: Indo para a proxima pagina...");
            page++;
        }

        if(!todosCarePacks.Any())
        {
            Console.WriteLine("[INFO]: Nenhum carepack encontrado.");
            return null;
        }

        Console.WriteLine($"[INFO]: Encontrados {todosCarePacks.Count} carepacks.");

        return todosCarePacks;
    }

    private static async Task DeletarTodosCarePacks(List<WooProduct> carePacks, List<WooProduct> todosCarePacks)
    {            
        Console.WriteLine($"[INFO]: Deletando todos os carepacks...");
        var todosIds = new List<int>();

        foreach(var carePack in todosCarePacks)
        {
            if(carePacks.Any(c => c.sku == carePack.sku))
            {
                todosIds.Add(carePack.id ?? 0);
            }
        }

        for (int i = 0; i<todosIds.Count; i += 5)
        {
            var grupoIds = todosIds.Skip(i).Take(5);

            var tarefas = grupoIds.Select(id => DeletarCarePack(id)).ToArray();
            await Task.WhenAll(tarefas);

            Console.WriteLine($"[INFO]: Deletados {grupoIds.Count()} carepacks.");
            Thread.Sleep(1000);
        }
    }

    private static async Task DeletarCarePack(int id)
    {
        var response = await _http.DeleteAsync($"{WOOCOMMERCE_BASE_URL}/{id}?force=true");

        if(response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[INFO]: Carepack {id} deletado com sucesso.");
        }
        else
        {
            var erro = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[ERROR]: Erro ao deletar carepack {id}: {erro}");
        }
    }

    private static async Task EnviarCarePacksEmLotes(List<WooProduct> carePacks)
    {
        Console.WriteLine($"[INFO]: Enviando carepacks em lotes...");
        var lotes = new List<List<WooProduct>>();

        for(int i = 0; i < carePacks.Count; i += 10)
        {
            lotes.Add(carePacks.Skip(i).Take(10).ToList());
            Console.WriteLine($"[INFO]: Lote {i} de {carePacks.Count} criado.");
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

        Console.WriteLine("[INFO]: Todos os carepacks foram processados com sucesso.");
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