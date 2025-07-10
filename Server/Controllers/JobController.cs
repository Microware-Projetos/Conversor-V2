using Microsoft.AspNetCore.Mvc;
using LiteDB;
using eCommerce.Shared.Models;

namespace eCommerce.Server.Controllers;

[ApiController]
[Route("api/job")]
public class JobController : ControllerBase
{
    [HttpPost]
    public IActionResult CriarJob([FromForm] IFormFile produto, [FromForm] IFormFile preco)
    {
        // Salvar arquivos em disco
        var pasta = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        Directory.CreateDirectory(pasta);

        var caminhoProduto = Path.Combine(pasta, Guid.NewGuid() + "_" + produto.FileName);
        var caminhoPreco = Path.Combine(pasta, Guid.NewGuid() + "_" + preco.FileName);

        using (var stream = new FileStream(caminhoProduto, FileMode.Create))
            produto.CopyTo(stream);
        using (var stream = new FileStream(caminhoPreco, FileMode.Create))
            preco.CopyTo(stream);

        // Criar job no LiteDB
        using var db = new LiteDatabase("Filename=fila.db;Connection=shared");
        var col = db.GetCollection<JobFila>("jobs");

        var job = new JobFila
        {
            Id = ObjectId.NewObjectId(),
            CaminhoArquivoProduto = caminhoProduto,
            CaminhoArquivoPreco = caminhoPreco
        };

        col.Insert(job);

        return Ok(new JobFilaResponse
        {
            Id = job.IdString,
            Status = job.Status,
            Tipo = job.Tipo,
            DataCriacao = job.DataCriacao,
            CaminhoArquivoProduto = job.CaminhoArquivoProduto,
            CaminhoArquivoPreco = job.CaminhoArquivoPreco,
            Mensagem = job.Mensagem
        });
    }

    [HttpGet]
    public ActionResult<List<JobFilaResponse>> ListarJobs()
    {
        using var db = new LiteDatabase("Filename=fila.db;Connection=shared");
        var col = db.GetCollection<JobFila>("jobs");
        
        var jobs = col.FindAll().Select(job => new JobFilaResponse
        {
            Id = job.IdString,
            Status = job.Status,
            Tipo = job.Tipo,
            DataCriacao = job.DataCriacao,
            CaminhoArquivoProduto = job.CaminhoArquivoProduto,
            CaminhoArquivoPreco = job.CaminhoArquivoPreco,
            Mensagem = job.Mensagem
        }).ToList();
        
        return Ok(jobs);
    }

    [HttpGet("{id}")]
    public ActionResult<JobFilaResponse> ConsultarJob(string id)
    {
        using var db = new LiteDatabase("Filename=fila.db;Connection=shared");
        var col = db.GetCollection<JobFila>("jobs");
        
        try
        {
            var objectId = new ObjectId(id);
            var job = col.FindById(objectId);
            if (job == null) return NotFound();
            
            return Ok(new JobFilaResponse
            {
                Id = job.IdString,
                Status = job.Status,
                Tipo = job.Tipo,
                DataCriacao = job.DataCriacao,
                CaminhoArquivoProduto = job.CaminhoArquivoProduto,
                CaminhoArquivoPreco = job.CaminhoArquivoPreco,
                Mensagem = job.Mensagem
            });
        }
        catch (Exception)
        {
            return BadRequest("ID inválido");
        }
    }
}