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
                            await HPProductProcessor.ProcessarListasProdutos(job.CaminhoArquivoProduto, job.CaminhoArquivoPreco);
                            break;
                    }
                    job.Status = StatusJob.Concluido;
                    job.Mensagem = "Processamento finalizado com sucesso!";
                    LimparArquivosExcel(job);
                }
                catch (Exception ex)
                {
                    job.Status = StatusJob.Erro;
                    job.Mensagem = $"Erro: {ex.Message}";
                }

                col.Update(job);
            }
            else
            {
                await Task.Delay(2000); // Aguarda antes de checar novamente
            }
        }
    }

    private void LimparArquivosExcel(JobFila job)
    {
        try
        {
            // Limpar arquivo de produtos
            if (!string.IsNullOrEmpty(job.CaminhoArquivoProduto) && File.Exists(job.CaminhoArquivoProduto))
            {
                File.Delete(job.CaminhoArquivoProduto);
                Console.WriteLine($"Arquivo de produtos removido: {job.CaminhoArquivoProduto}");
            }
            
            // Limpar arquivo de preços
            if (!string.IsNullOrEmpty(job.CaminhoArquivoPreco) && File.Exists(job.CaminhoArquivoPreco))
            {
                File.Delete(job.CaminhoArquivoPreco);
                Console.WriteLine($"Arquivo de preços removido: {job.CaminhoArquivoPreco}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao limpar arquivos Excel: {ex.Message}");
        }
    }
}