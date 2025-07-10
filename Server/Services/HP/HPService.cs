using Newtonsoft.Json.Linq;
using eCommerce.Shared.Models;
using LiteDB;

namespace eCommerce.Server.Services.HP;

public class HPService
{
    private readonly LiteDatabase _db;
   
    public HPService(LiteDatabase db)
    {
        _db = db;
    }

    public async Task<JobFilaResponse> EnviarProdutos(IFormFile arquivoProdutos, IFormFile arquivoPrecos)
    {
        // Salvar arquivos em disco
        var pasta = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        Directory.CreateDirectory(pasta);

        var caminhoProduto = Path.Combine(pasta, Guid.NewGuid() + "_" + arquivoProdutos.FileName);
        var caminhoPreco = Path.Combine(pasta, Guid.NewGuid() + "_" + arquivoPrecos.FileName);

        using (var stream = new FileStream(caminhoProduto, FileMode.Create))
            await arquivoProdutos.CopyToAsync(stream);
        using (var stream = new FileStream(caminhoPreco, FileMode.Create))
            await arquivoPrecos.CopyToAsync(stream);

        var objectId = ObjectId.NewObjectId();
        Console.WriteLine($"ObjectId gerado: {objectId}");

        var job = new JobFila
        {
            Id = objectId,
            Tipo = TipoJob.Produtos,
            Status = StatusJob.Pendente,
            DataCriacao = DateTime.Now,
            CaminhoArquivoProduto = caminhoProduto,
            CaminhoArquivoPreco = caminhoPreco
        };
        
        Console.WriteLine($"Job antes de salvar - Id: {job.Id}, IdString: {job.IdString}");
        
        _db.GetCollection<JobFila>("jobs").Insert(job);
        
        Console.WriteLine($"Job após salvar - Id: {job.Id}, IdString: {job.IdString}");
        
        return new JobFilaResponse
        {
            Id = job.IdString,
            Status = job.Status,
            Tipo = job.Tipo,
            DataCriacao = job.DataCriacao,
            CaminhoArquivoProduto = job.CaminhoArquivoProduto,
            CaminhoArquivoPreco = job.CaminhoArquivoPreco,
            Mensagem = job.Mensagem
        };
    }
}