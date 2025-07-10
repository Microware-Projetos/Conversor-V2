using LiteDB;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using eCommerce.Shared.Models;
using eCommerce.Server.Processors.HP;

namespace eCommerce.Server.Services.Job;

public class JobWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var db = new LiteDatabase("Filename=fila.db;Connection=shared");
            var col = db.GetCollection<JobFila>("jobs");
            var job = col.FindOne(x => x.Status == StatusJob.Pendente);
            if (job != null)
            {
                job.Status = StatusJob.Processando;
                col.Update(job);

                try
                {
                    switch (job.Tipo)
                    {
                        case TipoJob.Produtos:
                            HPProductProcessor.ProcessarListasProdutos(job.CaminhoArquivoProduto, job.CaminhoArquivoPreco);
                            break;
                    }
                    job.Status = StatusJob.Concluido;
                    job.Mensagem = "Processamento finalizado com sucesso!";
                }
                catch (Exception ex)
                {
                    job.Status = StatusJob.Erro;
                    job.Mensagem = $"Erro: {ex.Message}";
                }

                
                col.Update(job);

                job.Status = StatusJob.Concluido;
                job.Mensagem = "Processamento finalizado com sucesso!";
                col.Update(job);
            }
            else
            {
                await Task.Delay(2000); // Aguarda antes de checar novamente
            }
        }
    }
}