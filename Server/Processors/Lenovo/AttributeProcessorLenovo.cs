using eCommerce.Shared.Models;
using WooAttribute = eCommerce.Shared.Models.Attribute;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace eCommerce.Server.Processors.Lenovo;

public static class AttributeProcessorLenovo
{
    public static List<WooAttribute> ProcessAttributes(string sku, string model, IXLRow product, IXLRow cabecalho, Dictionary<string, string> normalizedAnatel, Dictionary<string, string> normalizedFamily)
    {
        var attributes = new List<WooAttribute>();

        var attributesMapping = JsonConvert
            .DeserializeObject<List<Dictionary<string, string>>>(File.ReadAllText("/app/eCommerce/Server/Maps/Lenovo/attributesLenovo.json"))?[0]
            ?? new Dictionary<string, string>();

        var attributesMappingWp = JsonConvert
            .DeserializeObject<List<AttributeMap>>(File.ReadAllText("/app/eCommerce/Server/Maps/Lenovo/attributesWordpress.json"))
            ?? new List<AttributeMap>();

        // Família e Anatel
        normalizedFamily.TryGetValue(model, out var family);
        normalizedAnatel.TryGetValue(family ?? "", out var anatel);

        if (!string.IsNullOrWhiteSpace(anatel))
        {
            attributes.Add(new WooAttribute
            {
                id = 12,
                options = anatel,
                visible = true
            });
        }
        else
        {
            Console.WriteLine($"[INFO]: {sku} - Produto sem codigo anatel");
        }

        // Mapear colunas da planilha para seus valores
        var cellDict = new Dictionary<string, string>();
        for (int i = 1; i <= cabecalho.CellCount(); i++)
        {
            var colName = cabecalho.Cell(i).GetString().Trim();
            var valor = product.Cell(i).GetString().Trim();
            if (!cellDict.ContainsKey(colName))
            {
                cellDict[colName] = valor;
            }
        }

        // Processar os atributos mapeados
        foreach (var (lenovoKey, wpName) in attributesMapping)
        {
            if (!cellDict.TryGetValue(lenovoKey, out var rawValue))
                continue;

            if (string.IsNullOrWhiteSpace(rawValue) || rawValue.Trim().ToLower() == "nan")
                continue;

            var wpAttribute = attributesMappingWp.FirstOrDefault(a => a.name == wpName);
            if (wpAttribute == null)
                continue;

            var existingAttribute = attributes.FirstOrDefault(a => a.id == wpAttribute.id);

            if (existingAttribute != null)
            {
                // Evita duplicidade se valor já for igual
                if (!string.Equals(existingAttribute.options, rawValue, StringComparison.OrdinalIgnoreCase))
                {
                    // Concatena múltiplos valores como string, separados por vírgula
                    existingAttribute.options += $", {rawValue}";
                }
            }
            else
            {
                attributes.Add(new WooAttribute
                {
                    id = wpAttribute.id,
                    options = rawValue,
                    visible = true
                });
            }
        }

        // Adiciona modelo (PH4_DESCRIPTION)
        attributes.Add(new WooAttribute
        {
            id = 68,
            options = model,
            visible = true
        });

        // Adiciona marca "Lenovo"
        attributes.Add(new WooAttribute
        {
            id = 67,
            options = "Lenovo",
            visible = true
        });

        return attributes;

    }
} 