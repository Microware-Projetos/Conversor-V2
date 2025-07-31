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
    public static List<WooAttribute> ProcessarAttributes(string sku, string model, IXLRow linha, IXLRow cabecalho, Dictionary<string, string> prodInfo, string sheetName, Dictionary<string, string> normalizedAnatel, Dictionary<string, string> normalizedFamily)
    {
        var attributes = new List<WooAttribute>();

        var mapData = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText("Maps/Lenovo/map.json"));
        var colunasMapping = mapData?.Colunas?.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>();
        var attributesMapping = mapData?.Attributes?.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>();
        var attributesMappingWp = JsonConvert.DeserializeObject<List<AttributeMap>>(File.ReadAllText("Maps/Lenovo/attributesWordpress.json")) ?? new List<AttributeMap>();

        // Usar os valores normalizados passados como parâmetros
        string family = "";
        string anatel = "";
        
        // Busca por contains no normalizedFamily
        foreach (var kvp in normalizedFamily)
        {
            if (model.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                family = kvp.Value;
                break;
            }
        }
        
        // Se não encontrou family, tenta buscar por SKU
        if (string.IsNullOrWhiteSpace(family))
        {
            foreach (var kvp in normalizedFamily)
            {
                if (sku.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    family = kvp.Value;
                    break;
                }
            }
        }
        
        // Busca anatel baseado na family encontrada
        if (!string.IsNullOrWhiteSpace(family))
        {
            foreach (var kvp in normalizedAnatel)
            {
                if (family.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    anatel = kvp.Value;
                    break;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(anatel))
        {
            attributes.Add(new WooAttribute { id = 12, options = anatel, visible = true });
        }

        // Percorrer colunas por nome (como no Python)
        for (int i = 1; i <= cabecalho.CellCount(); i++)
        {
            string hp_key = cabecalho.Cell(i).GetString().Trim() ?? "";
            string valorCelula = linha.Cell(i).GetString().Trim() ?? "";

            // Encontra o valor correspondente em Colunas
            if (colunasMapping.TryGetValue(hp_key, out string coluna_value))
            {
                if (coluna_value == "attributes")
                {
                    string attribute_name;
                    if (attributesMapping.TryGetValue(hp_key, out attribute_name) && !string.IsNullOrWhiteSpace(attribute_name))
                    {
                        var wp_attribute = attributesMappingWp?.FirstOrDefault(attr => attr.name == attribute_name);
                        if (wp_attribute != null)
                        {
                            string valor = linha.Cell(i).GetString().Trim() ?? "";
                            if (valor.ToLower() != "nan" && !string.IsNullOrWhiteSpace(valor))
                            {
                                var atributo_existente = attributes.FirstOrDefault(attr => attr.id == wp_attribute.id);
                                if (atributo_existente != null)
                                {
                                    if (atributo_existente.options != valor)
                                    {
                                        atributo_existente.options = valor;
                                    }
                                }
                                else
                                {
                                    attributes.Add(new WooAttribute
                                    {
                                        id = wp_attribute.id,
                                        options = valor,
                                        visible = true
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }

        // Adicionar EAN e Marca
        if (prodInfo.TryGetValue("EAN", out var ean) && !string.IsNullOrWhiteSpace(ean))
        {
            attributes.Add(new WooAttribute { id = 13, options = ean, visible = true });
        }

        attributes.Add(new WooAttribute { id = 45, options = "Lenovo", visible = true });

        return attributes;
    }
} 