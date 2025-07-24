namespace eCommerce.Shared.Models;

// Classes auxiliares para deserialização dos JSONs
public class WordPressCategory
{
    public int id { get; set; }
    public string name { get; set; } = "";
}

public class BlingCategory
{
    public long id { get; set; }
    public string descricao { get; set; } = "";
    public CategoriaPai categoriaPai { get; set; } = new();
}

public class CategoriaPai
{
    public long id { get; set; }
}

// Classes para deserializar resposta da API do Bling
public class BlingProdutoResponse
{
    public BlingProdutoData? data { get; set; }
}

public class BlingProdutoData
{
    public long id { get; set; }
    public object? variations { get; set; }
    public List<string>? warnings { get; set; }
}

public class BlingProdutoCompletoResponse
{
    public BlingProdutoCompletoData? data { get; set; }
}

public class BlingProdutoCompletoData
{
    public long id { get; set; }
    public string? nome { get; set; }
    public string? codigo { get; set; }
    public decimal? preco { get; set; }
    public List<BlingImagemURLVerificacao>? imagens { get; set; }
    public BlingEstoqueVerificacao? estoque { get; set; }
}

public class BlingImagemURLVerificacao
{
    public string? link { get; set; }
}

public class BlingEstoqueVerificacao
{
    public int? quantidade { get; set; }
} 