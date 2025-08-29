using System;
using LiteDB;

namespace eCommerce.Shared.Models;
   
public class JobFila
{
    [BsonId]
    public ObjectId Id { get; set; }
    
    [BsonIgnore]
    public string IdString => Id.ToString();
    
    public StatusJob Status { get; set; } = StatusJob.Pendente;
    public TipoJob Tipo { get; set; } = TipoJob.Produtos;
    public FornecedorJob Fornecedor { get; set; }
    public DateTime DataCriacao { get; set; } = DateTime.Now;
    public DateTime? DataFinalizacao { get; set; }
    public string CaminhoArquivoProduto { get; set; } = string.Empty;
    public string? CaminhoArquivoPreco { get; set; }
    public string? Mensagem { get; set; }
}

public class JobFilaResponse
{
    public string Id { get; set; } = string.Empty;
    public StatusJob Status { get; set; }
    public TipoJob Tipo { get; set; }
    public FornecedorJob Fornecedor { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime? DataFinalizacao { get; set; }
    public string CaminhoArquivoProduto { get; set; } = string.Empty;
    public string? CaminhoArquivoPreco { get; set; }
    public string? Mensagem { get; set; }
}

public enum TipoJob {
    Produtos,
    Plotter,
    CarePack,
    Promocao,
    Base
}

public enum StatusJob {
    Pendente,
    Processando,
    Concluido,
    Erro
}

//Enumerate para identificar o fornecedor do job
public enum FornecedorJob{
    HP,
    Lenovo,
    Base,
    Cisco,
    Microware
}