using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Linq;
using eCommerce.Server.Processors.HP.Helpers;
using eCommerce.Server.Processors.HP;

namespace eCommerce.Server.Processors.HP;

public static class ExcelProcessorHP
{
    public static Dictionary<string, string> GetPrecosPorSku(string caminhoArquivoPrecos)
    {
        var precosPorSku = new Dictionary<string, string>();
        var listPrecos = new XLWorkbook(caminhoArquivoPrecos);

        foreach (var ws in listPrecos.Worksheets)
        {
            var linhasPrecos = ws.RowsUsed().Skip(1);

            foreach (var linha in linhasPrecos)
            {
                var sku = linha.Cell(2).GetString().Trim();
                var precoStr = linha.Cell(14).GetString().Trim();
                var icms = linha.Cell(16).GetString().Trim();
                var ean = linha.Cell(10).GetString().Trim();
                var ncm = linha.Cell(11).GetString().Trim();

                if (string.IsNullOrWhiteSpace(sku)) continue;

                // Tentar converter o preço para decimal
                if (!decimal.TryParse(precoStr, out decimal precoAtual)) continue;

                // Se o SKU ainda não existe ou o novo preço for maior, atualiza
                if (!precosPorSku.ContainsKey(sku) || decimal.Parse(precosPorSku[sku]) < precoAtual)
                {
                    precosPorSku[sku] = precoStr;
                    precosPorSku[sku + "_icms"] = icms;
                    precosPorSku[sku + "_ean"] = ean;
                    precosPorSku[sku + "_ncm"] = ncm;
                }
            }
        }

        return precosPorSku;
    }
} 