namespace eCommerce.Shared.Models;

// Classes auxiliares para deserialização dos JSONs
public class WordPressCategory
{
    public int id { get; set; }
    public string name { get; set; } = "";
}

public class BaseCategory
{
    public long id { get; set; }
    public string descricao { get; set; } = "";
    public CategoriaPai categoriaPai { get; set; } = new();
}

public class CategoriaPai
{
    public long id { get; set; }
}

// Classes para deserializar resposta da API do Base
public class BaseProdutoResponse
{
    public BaseProdutoData? data { get; set; }
}

public class BaseProdutoData
{
    public long id { get; set; }
    public object? variations { get; set; }
    public List<string>? warnings { get; set; }
}

public class BaseProdutoCompletoResponse
{
    public BaseProdutoCompletoData? data { get; set; }
}

public class BaseProdutoCompletoData
{
    public long id { get; set; }
    public string? nome { get; set; }
    public string? codigo { get; set; }
    public decimal? preco { get; set; }
    public List<BaseImagemURLVerificacao>? imagens { get; set; }
    public BaseEstoqueVerificacao? estoque { get; set; }
}

public class BaseImagemURLVerificacao
{
    public string? link { get; set; }
}

public class BaseEstoqueVerificacao
{
    public int? quantidade { get; set; }
} 