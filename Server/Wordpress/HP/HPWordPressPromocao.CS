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

public static class HPWordPressPromocao
{
    private const string WOOCOMMERCE_BASE_URL = "https://ecommerce.microware.com.br/hp/wp-json/wc/v3/products";
    private const string WOOCOMMERCE_CONSUMER_KEY = "ck_3e315f613482b1c092d5304ac8cd95e4c28806d1";
    private const string WOOCOMMERCE_CONSUMER_SECRET = "cs_7ab0dd32ba84a9ccfe28a53ba3ad1daa94415488";

    private static readonly HttpClient _http = new HttpClient();

    public static async Task<WooProduct> BuscarProduto(string sku)
    {
        Console.WriteLine($"[INFO]: Buscando produto {sku} no WooCommerce...");
        
        var byteArray = Encoding.ASCII.GetBytes($"{WOOCOMMERCE_CONSUMER_KEY}:{WOOCOMMERCE_CONSUMER_SECRET}");
        var authHeader = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

        _http.DefaultRequestHeaders.Authorization = authHeader;
        
        try
        {
            var response = await _http.GetAsync($"{WOOCOMMERCE_BASE_URL}?sku={sku}");
            var content = await response.Content.ReadAsStringAsync();

            var produto = JsonConvert.DeserializeObject<WooProduct>(content);

            if(produto.sku == sku)
            {
                Console.WriteLine($"[INFO]: Produto {sku} encontrado no WooCommerce.");
                return produto;
            }
            else
            {
                Console.WriteLine($"[INFO]: Produto {sku} não encontrado no WooCommerce.");
                return null;
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"[ERROR]: Erro ao buscar produto {sku}: {ex.Message}");
            return null;
        }
    }

    public static async Task AtualizarProduto(int produtoId, Dictionary<string, string> data)
    {
        Console.WriteLine($"[INFO]: Atualizando produto {produtoId} no WooCommerce...");
        var byteArray = Encoding.ASCII.GetBytes($"{WOOCOMMERCE_CONSUMER_KEY}:{WOOCOMMERCE_CONSUMER_SECRET}");
        var authHeader = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

        _http.DefaultRequestHeaders.Authorization = authHeader;
        try
        {
            var url = $"{WOOCOMMERCE_BASE_URL}/{produtoId}";
            var json = JsonConvert.SerializeObject(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PutAsync(url, content);

            if(response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[INFO]: Produto {produtoId} atualizado com sucesso.");
            }
            else
            {
                Console.WriteLine($"[ERROR]: Erro ao atualizar produto {produtoId}: {response.StatusCode}");
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"[ERROR]: Erro ao atualizar produto {produtoId}: {ex.Message}");
        }
    }
}