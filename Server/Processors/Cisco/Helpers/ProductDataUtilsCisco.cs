using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ClosedXML.Excel;
using WooAttribute = eCommerce.Shared.Models.Attribute;
using eCommerce.Server.Helpers;
using eCommerce.Shared.Models;
using eCommerce.Server.Processors.Cisco.Helpers;

namespace eCommerce.Server.Processors.Cisco.Helpers;

public static class ProductDataUtilsCisco
{
    public static double GetValueStringToDouble(string value)
    {
        Console.WriteLine($"[INFO]: Convertendo valor para double: {value}");
        try
        {
            double price = double.Parse(value.Replace("R$", "").Replace(".", "").Replace(",", "."));
            Console.WriteLine($"[INFO]: Valor convertido com sucesso: {price}");
            return price;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR]: Erro ao converter valor: {ex.Message}");
            
            return 0;
        }
    } 

    public static string GetLeadtime(string icms)
    {
        Console.WriteLine($"[INFO]: Iniciando processo de leadtime: {icms}");
        try
        {
            double icmsDouble = GetValueStringToDouble(icms);
            Console.WriteLine($"[INFO]: ICMS convertido com sucesso: {icmsDouble}");

            if (icmsDouble == 4.00 || icmsDouble == 18.00)
            {
                Console.WriteLine($"[INFO]: ICMS é 4.00 ou 18.00, retornando importado");
                return "importado";
            }
            else if (icmsDouble == 7.00 || icmsDouble == 12.00)
            {
                Console.WriteLine($"[INFO]: ICMS é 7.00 ou 12.00, retornando local");
                return "local";
            }
            else
                return "local";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR]: Erro ao converter ICMS, retornando 'local': {ex.Message}");
            return "local";
        }
    }

    public static string ClearSku(string sku)
    {
        Console.WriteLine($"[INFO]: Iniciando processo de limpeza de SKU: {sku}");

        if (string.IsNullOrEmpty(sku))
        {
            Console.WriteLine($"[INFO]: SKU é nulo ou vazio, retornando string vazia");
            return string.Empty;
        }

        sku = sku.Trim();

        if (sku.EndsWith("_US"))
        {
            Console.WriteLine($"[INFO]: SKU termina com _US, removendo");
            sku = sku.Replace("_US", "");
        }

        if (sku.EndsWith("="))
        {
            Console.WriteLine($"[INFO]: SKU termina com =, removendo");
            sku = sku.Replace("=", "");
        }

        sku = sku.Trim();
        
        Console.WriteLine($"[INFO]: SKU limpo: {sku}");

        return sku;
    }

    public static List<MetaData> GetPhotos()
    {
        var urlPadrao = "https://eprodutos-integracao.microware.com.br/api/photos/image/6862fad32f55a6aba4458131.png";
        
        var photos = new List<MetaData>();

        photos.Add(new MetaData{ key = "_external_image_url", value = urlPadrao });

        return photos;
    }

    public static List<Category> GetCategories(string ncm)
    {
        var categories = new List<Category>();
        //OQUE É peoduct.get("Categoria")?
        //if (string.IsNullOrEmpty(categoria) || categoria.Trim() == "")
        //{
            if (ncm == "Licença")
                categories.Add(new Category{ id = 28});
            else
                categories.Add(new Category{ id = 29});
            
            return categories;
        //}
    }

    public static List<WooAttribute> GetAttributes(string ncm, string leadtime)
    {
        var attributes = new List<WooAttribute>();
        
        attributes.Add(new WooAttribute
        {
            id = 95,
            options = leadtime,
            visible = true
        });

        attributes.Add(new WooAttribute
        {
            id = 118,
            options = "Cisco",
            visible = true
        });

        if (ncm != "Licença")
        {
            attributes.Add(new WooAttribute
            {
                id = 94,
                options = ncm,
                visible = true
            });
        }

        return attributes;
    }

}