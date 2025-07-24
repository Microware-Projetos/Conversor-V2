using Microsoft.AspNetCore.Mvc;
using LiteDB;
using eCommerce.Shared.Models;
using eCommerce.Server.Services.Job;
using eCommerce.Server.Helpers;

namespace eCommerce.Server.Controllers;

[ApiController]
[Route("api/job")]
public class JobController : ControllerBase
{
    private readonly JobWorker _jobWorker;

    public JobController(JobWorker jobWorker)
    {
        _jobWorker = jobWorker;
    }

    [HttpPost]
    public IActionResult CriarJob([FromForm] IFormFile produto, [FromForm] IFormFile preco)
    {
        var pasta = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        var caminhoProduto = FileHelper.SaveFile(produto, pasta);
        var caminhoPreco = FileHelper.SaveFile(preco, pasta);

        using var db = new LiteDatabase("Filename=fila.db;Connection=shared");
        var job = new JobFila
        {
            Id = ObjectId.NewObjectId(),
            CaminhoArquivoProduto = caminhoProduto,
            CaminhoArquivoPreco = caminhoPreco
        };
        DbHelper.InsertOrUpdate(db, "jobs", job);

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

    [HttpPost("cancelar-atual")]
    public IActionResult CancelarJobAtual()
    {
        try
        {
            _jobWorker.CancelarJobAtual();
            return Ok(new { message = "Job em execução foi cancelado com sucesso." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "Erro ao cancelar job", error = ex.Message });
        }
    }

    [HttpDelete("limpar-todos")]
    public IActionResult LimparTodosJobs()
    {
        try
        {
            using var db = new LiteDatabase("Filename=fila.db;Connection=shared");
            var col = db.GetCollection<JobFila>("jobs");
            
            // Contar quantos jobs existem antes de limpar
            var totalJobs = col.Count();
            var jobsProcessando = col.Count(x => x.Status == StatusJob.Processando);
            
            // Cancelar job em execução se houver
            if (jobsProcessando > 0)
            {
                _jobWorker.CancelarJobAtual();
            }
            
            // Limpar todos os jobs
            col.DeleteAll();
            
            var mensagem = jobsProcessando > 0 
                ? $"Todos os {totalJobs} jobs foram removidos com sucesso. {jobsProcessando} job(s) em execução foi(foram) cancelado(s)."
                : $"Todos os {totalJobs} jobs foram removidos com sucesso.";
            
            return Ok(new { 
                message = mensagem,
                jobsRemovidos = totalJobs,
                jobsCancelados = jobsProcessando
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { 
                message = "Erro ao limpar jobs", 
                error = ex.Message 
            });
        }
    }
}