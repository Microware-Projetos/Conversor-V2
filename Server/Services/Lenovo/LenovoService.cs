using Newtonsoft.Json.Linq;
using eCommerce.Shared.Models;
using LiteDB;
using Microsoft.AspNetCore.Http;

namespace eCommerce.Server.Services.Lenovo;

public class LenovoService
{
    private readonly LiteDatabase _db;

    public LenovoService(LiteDatabase db)
    {
        _db = db;
    }

    public async Task<JobFilaResponse> EnviarProdutos(IFormFile arquivoProdutos)
    {
        // Salvar arquivos em disco
        var pasta = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        Directory.CreateDirectory(pasta);

        var caminhoProduto = Path.Combine(pasta, Guid.NewGuid() + "_" + arquivoProdutos.FileName);

        using (var stream = new FileStream(caminhoProduto, FileMode.Create))
            await arquivoProdutos.CopyToAsync(stream);

        var objectId = ObjectId.NewObjectId();
        Console.WriteLine($"ObjectId gerado: {objectId}");

        var job = new JobFila
        {
            Id = objectId,
            Status = StatusJob.Pendente,
            Tipo = TipoJob.Produtos,
            Fornecedor = FornecedorJob.Lenovo,
            DataCriacao = DateTime.Now,
            CaminhoArquivoProduto = caminhoProduto
        };

        Console.WriteLine($"Job antes de salvar - Id: {job.Id}, IdString: {job.IdString}");

        _db.GetCollection<JobFila>("jobs").Insert(job);

        Console.WriteLine($"Job após salvar - Id: {job.Id}, IdString: {job.IdString}");

        return new JobFilaResponse
        {
            Id = job.IdString,
            Status = job.Status,
            Tipo = job.Tipo,
            Fornecedor = job.Fornecedor,
            DataCriacao = job.DataCriacao,
            CaminhoArquivoProduto = job.CaminhoArquivoProduto,
            Mensagem = job.Mensagem
        };
    }
}