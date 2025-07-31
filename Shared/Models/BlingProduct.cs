using LiteDB;
using Newtonsoft.Json;

namespace eCommerce.Shared.Models;

public class BlingProduct
{
    [JsonProperty("nome")]
    public string Nome { get; set; } = "";
    
    [JsonProperty("tipo")]
    public string Tipo { get; set; } = "P";
    
    [JsonProperty("situacao")]
    public string Situacao { get; set; } = "A";
    
    [JsonProperty("formato")]
    public string Formato { get; set; } = "S";
    
    [JsonProperty("codigo")]
    public string Codigo { get; set; } = "";
    
    [JsonProperty("preco")]
    public decimal Preco { get; set; }
    
    [JsonProperty("gtin")]
    public string Gtin { get; set; } = "";

    [JsonProperty("descricaoCurta")]
    public string DescricaoCurta { get; set; } = "";
    
    [JsonProperty("unidade")]
    public string Unidade { get; set; } = "UN";
    
    [JsonProperty("pesoLiquido")]
    public decimal PesoLiquido { get; set; }
    
    [JsonProperty("pesoBruto")]
    public decimal PesoBruto { get; set; }
    
    [JsonProperty("volumes")]
    public int Volumes { get; set; } = 1;
    
    [JsonProperty("tipoProducao")]
    public string TipoProducao { get; set; } = "T";
    
    [JsonProperty("condicao")]
    public int Condicao { get; set; } = 0;
    
    [JsonProperty("freteGratis")]
    public bool FreteGratis { get; set; } = false;
    
    [JsonProperty("marca")]
    public string Marca { get; set; } = "";
    
    [JsonProperty("descricaoComplementar")]
    public string DescricaoComplementar { get; set; } = "";
    
    [JsonProperty("linkExterno")]
    public string LinkExterno { get; set; } = "";
    
    [JsonProperty("observacoes")]
    public string Observacoes { get; set; } = "";
    
    [JsonProperty("descricaoEmbalagemDiscreta")]
    public string DescricaoEmbalagemDiscreta { get; set; } = "";
    
    [JsonProperty("categoria")]
    public BlingCategoria Categoria { get; set; } = new();
    
    [JsonProperty("estoque")]
    public BlingEstoque Estoque { get; set; } = new();
    
    [JsonProperty("actionEstoque")]
    public string ActionEstoque { get; set; } = "T";
    
    [JsonProperty("dimensoes")]
    public BlingDimensoes Dimensoes { get; set; } = new();
    
    [JsonProperty("midia")]
    public BlingMidia Midia { get; set; } = new();
    
    [JsonProperty("dataCriacao")]
    public DateTime DataCriacao { get; set; } = DateTime.Now;
    
    [JsonProperty("dataAtualizacao")]
    public DateTime DataAtualizacao { get; set; } = DateTime.Now;
    
    [JsonProperty("camposCustomizados")]
    public List<BlingCampoCustomizado>? CamposCustomizados { get; set; }
}

public class BlingCategoria
{
    [JsonProperty("id")]
    public long Id { get; set; }
}

public class BlingEstoque
{
    [JsonProperty("minimo")]
    public int Minimo { get; set; }
    
    [JsonProperty("maximo")]
    public int Maximo { get; set; }

    [JsonProperty("crossdocking")]
    public int CrossDocking { get; set; }
}

public class BlingDimensoes
{
    [JsonProperty("largura")]
    public decimal Largura { get; set; }
    
    [JsonProperty("altura")]
    public decimal Altura { get; set; }
    
    [JsonProperty("profundidade")]
    public decimal Profundidade { get; set; }
    
    [JsonProperty("unidadeMedida")]
    public int UnidadeMedida { get; set; }
}

public class BlingMidia
{
    [JsonProperty("imagens")]
    public BlingImagens Imagens { get; set; } = new();
}

public class BlingImagens
{
    [JsonProperty("imagensURL")]
    public List<BlingImagemURL> ImagensURL { get; set; } = new();
}

public class BlingImagemURL
{
    [JsonProperty("link")]
    public string Link { get; set; } = "";
}
