using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using ClosedXML.Excel;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using WooAttribute = eCommerce.Shared.Models.Attribute;
using eCommerce.Server.Helpers;
using eCommerce.Shared.Models;

namespace eCommerce.Server.Processors.Lenovo;

public static class CarePackDataUtilsLenovo
{
    private static readonly string CATEGORIES_PATH_FILE ="/app/eCommerce/Server/Maps/Lenovo/categoriesWordpress.json";
    
    public static List<MetaData> ProcessPhotos()
    {
        Console.WriteLine("[INFO]: Processando fotos");
        var metaData = new MetaData()
        {
            key = "_external_image_url",
            value = "https://eprodutos-integracao.microware.com.br/api/photos/image/67c1e66abe14dc12f6b266e2.png"
        };

        return new List<MetaData>{ metaData };
    }

    public static Dimensions ProcessDimensions()
    {
        Console.WriteLine("[INFO]: Processando dimensões");
        return new Dimensions()
        {
            length = "0",
            width = "0",
            height = "0"
        };
    }

    public static string ProcessWeight()
    {
        Console.WriteLine("[INFO]: Processando peso");
        return "0";
    }

    public static List<WooAttribute> ProcessAttributes(IXLRow product)
    {
        Console.WriteLine("[INFO]: Processando atributos");
        var attributes = new List<WooAttribute>
        {
            new WooAttribute
            {
                id = 550,
                options = product.Cell(6).Value.ToString(),
                visible = true
            },
            new WooAttribute
            {
                id = 14,
                options = product.Cell(3).Value.ToString(),
                visible = true
            },
            new WooAttribute
            {
                id = 9,
                options = "Serviço",
                visible = true
            }
        };

        return attributes;
    }

    public static List<Category> ProcessCategories()
    {
        Console.WriteLine("[INFO]: Processando categorias");
        var categories = new List<Category>();
        var json = File.ReadAllText(CATEGORIES_PATH_FILE);
        var categoriesMapping = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(json);

        var match = categoriesMapping?.FirstOrDefault(c => c.ContainsKey("name") && c["name"] == "Serviço");
        
        if(match != null && match.ContainsKey("id"))
        {
            Console.WriteLine("[INFO]: ID da categoria associado");
            categories.Add(new Category { id = int.Parse(match["id"]) });
        }

        return categories;
    }
    
}