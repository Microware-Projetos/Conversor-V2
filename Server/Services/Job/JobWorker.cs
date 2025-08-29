using LiteDB;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using eCommerce.Shared.Models;
using eCommerce.Server.Processors.HP;
using eCommerce.Server.Processors.Base;
using eCommerce.Server.Processors.Lenovo;

namespace eCommerce.Server.Services.Job;

public class JobWorker : BackgroundService
{
    private CancellationTokenSource? _currentJobCancellationSource;
    private readonly object _lockObject = new object();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var db = new LiteDatabase("Filename=fila.db;Connection=shared");
            var col = db.GetCollection<JobFila>("jobs");
            var job = col.FindOne(x => x.Status == StatusJob.Pendente);
            if (job != null)
            {
                Console.WriteLine($"Job encontrado - Tipo: {job.Tipo}, Produto: {job.CaminhoArquivoProduto}, Preço: {job.CaminhoArquivoPreco ?? "NULO"}");
                
                job.Status = StatusJob.Processando;
                col.Update(job);

                // Criar CancellationToken para este job específico
                lock (_lockObject)
                {
                    _currentJobCancellationSource = new CancellationTokenSource();
                }

                try
                {
                    // Combinar o CancellationToken do job com o stoppingToken
                    using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        _currentJobCancellationSource.Token, 
                        stoppingToken);

                    switch (job.Tipo)
                    {
                        case TipoJob.Produtos when job.Fornecedor == FornecedorJob.HP:
                            // Verifica se o arquivo de preços foi fornecido para jobs HP
                            if (string.IsNullOrEmpty(job.CaminhoArquivoPreco))
                            {
                                throw new InvalidOperationException("Arquivo de preços é obrigatório para processamento de produtos HP");
                            }
                            
                            Console.WriteLine($"[INFO]: Processando job HP - Produto: {job.CaminhoArquivoProduto}, Preço: {job.CaminhoArquivoPreco}");
                            await HPProductProcessor.ProcessarListasProdutos(job.CaminhoArquivoProduto, job.CaminhoArquivoPreco, combinedCts.Token);
                            break;

                        case TipoJob.CarePack when job.Fornecedor == FornecedorJob.HP:
                            Console.WriteLine($"\n[INFO]: Processando job HP - CarePack: {job.CaminhoArquivoProduto}");
                            await HPCarePackProcessor.ProcessarListaCarePack(job.CaminhoArquivoProduto, combinedCts.Token);
                            break;
                        
                        case TipoJob.Plotter when job.Fornecedor == FornecedorJob.HP:
                            Console.WriteLine($"\n[INFO]: Processando job HP - Plotter: {job.CaminhoArquivoProduto}");
                            await HPPlotterProcessor.ProcessarListaPlotter(job.CaminhoArquivoProduto, combinedCts.Token);
                            break;
                        
                        case TipoJob.Promocao when job.Fornecedor == FornecedorJob.HP:
                            Console.WriteLine($"\n[INFO]: Processando job HP - Promocao: {job.CaminhoArquivoProduto}");
                            await HPPromocaoProcessor.ProcessarListasPromocao(job.CaminhoArquivoProduto, combinedCts.Token);
                            break;

                        case TipoJob.Produtos when job.Fornecedor == FornecedorJob.Lenovo:
                            Console.WriteLine($"\n[INFO]: Processando job Lenovo - Produto: {job.CaminhoArquivoProduto}");
                            await LenovoProductProcessor.ProcessarListasProdutos(job.CaminhoArquivoProduto, combinedCts.Token);
                            break;
                        
                        case TipoJob.CarePack when job.Fornecedor == FornecedorJob.Lenovo:
                            Console.WriteLine($"\n[INFO]: Processando job Lenovo - CarePack: {job.CaminhoArquivoProduto}");
                            await LenovoCarePackProcessor.ProcessarListaCarePack(job.CaminhoArquivoProduto, combinedCts.Token);
                            break;

                        case TipoJob.Base:
                            await BaseProcessor.ProcessarProdutos(job.CaminhoArquivoProduto, combinedCts.Token);
                            break;
                    }
                    
                    // Verificar se foi cancelado
                    if (combinedCts.Token.IsCancellationRequested)
                    {
                        job.Status = StatusJob.Erro;
                        job.Mensagem = "Cancelado pelo usuário";
                    }
                    else
                    {
                        job.Status = StatusJob.Concluido;
                        job.Mensagem = "Finalizado com sucesso!";
                    }
                    
                    // Definir data de finalização
                    job.DataFinalizacao = DateTime.Now;
                    
                    LimparArquivosExcel(job);
                }
                catch (OperationCanceledException)
                {
                    job.Status = StatusJob.Erro;
                    job.Mensagem = "Cancelado pelo usuário";
                    job.DataFinalizacao = DateTime.Now;
                    LimparArquivosExcel(job);
                }
                catch (Exception ex)
                {
                    job.Status = StatusJob.Erro;
                    job.Mensagem = $"Erro: {ex.Message}";
                    job.DataFinalizacao = DateTime.Now;
                    LimparArquivosExcel(job);
                }
                finally
                {
                    lock (_lockObject)
                    {
                        _currentJobCancellationSource?.Dispose();
                        _currentJobCancellationSource = null;
                    }
                }

                col.Update(job);
            }
            else
            {
                await Task.Delay(2000, stoppingToken); // Aguarda antes de checar novamente
            }
        }
    }

    public void CancelarJobAtual()
    {
        lock (_lockObject)
        {
            _currentJobCancellationSource?.Cancel();
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
                Console.WriteLine($"[FINISH]: Arquivo de produtos removido: {job.CaminhoArquivoProduto} (Tipo: {job.Tipo}) - Data Finalização: {job.DataFinalizacao?.ToString("dd/MM/yyyy HH:mm")}");
            }
            
            // Limpar arquivo de preços (se existir)
            if (!string.IsNullOrEmpty(job.CaminhoArquivoPreco) && File.Exists(job.CaminhoArquivoPreco))
            {
                File.Delete(job.CaminhoArquivoPreco);
                Console.WriteLine($"Arquivo de preços removido: {job.CaminhoArquivoPreco} (Tipo: {job.Tipo})");
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao limpar arquivos Excel: {ex.Message}");
        }
    }
}