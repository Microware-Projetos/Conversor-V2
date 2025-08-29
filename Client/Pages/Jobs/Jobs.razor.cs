using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using eCommerce.Client.Services.Job;
using eCommerce.Shared.Models;
using System.Threading;

namespace eCommerce.Client.Pages.Jobs;

public partial class Jobs : ComponentBase
{
    [Inject]
    private JobService _jobService { get; set; } = default!;

    private List<JobFilaResponse> todosJobs = new List<JobFilaResponse>();
    private List<JobFilaResponse> jobsExibidos = new List<JobFilaResponse>();
    private List<JobFilaResponse> jobsFiltrados = new List<JobFilaResponse>();
    private bool isLoading = false;
    private string mensagem = "";
    private const int ITENS_POR_PAGINA = 9;
    private int paginaAtual = 1;
    private int totalPaginas = 1;
    private StatusJob? filtroStatus = null;
    private Timer? mensagemTimer;

    protected override async Task OnInitializedAsync()
    {
        await CarregarJobs();
    }

    private void LimparMensagem()
    {
        mensagem = "";
        CancelarTimerMensagem();
        StateHasChanged();
    }

    private void CancelarTimerMensagem()
    {
        mensagemTimer?.Dispose();
        mensagemTimer = null;
    }

    private void IniciarTimerMensagem()
    {
        CancelarTimerMensagem();
        mensagemTimer = new Timer(_ =>
        {
            InvokeAsync(() =>
            {
                mensagem = "";
                StateHasChanged();
            });
        }, null, 5000, Timeout.Infinite);
    }

    private async Task CarregarJobs()
    {
        try
        {
            isLoading = true;
            StateHasChanged();
            
            todosJobs = await _jobService.ListarJobs();
            jobsFiltrados = todosJobs.ToList();
            CalcularPaginas();
            AplicarPaginacao();
            mensagem = $"Carregados {todosJobs.Count} jobs";
            IniciarTimerMensagem();
        }
        catch (Exception ex)
        {
            mensagem = $"Erro ao carregar jobs: {ex.Message}";
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private void CalcularPaginas()
    {
        var totalJobs = jobsFiltrados.Count;
        totalPaginas = (int)Math.Ceiling((double)totalJobs / ITENS_POR_PAGINA);
        if (totalPaginas == 0) totalPaginas = 1;
        if (paginaAtual > totalPaginas) paginaAtual = totalPaginas;
    }

    private void AplicarPaginacao()
    {
        // Aplicar filtros primeiro
        AplicarFiltros();
        
        // Aplicar ordenação
        var jobsOrdenados = jobsFiltrados.OrderByDescending(j => j.DataCriacao).ToList();
        
        // Aplicar paginação
        var inicio = (paginaAtual - 1) * ITENS_POR_PAGINA;
        jobsExibidos = jobsOrdenados.Skip(inicio).Take(ITENS_POR_PAGINA).ToList();
        
        // Recalcular páginas
        CalcularPaginas();
    }

    private void IrParaPagina(int pagina)
    {
        if (pagina >= 1 && pagina <= totalPaginas)
        {
            paginaAtual = pagina;
            AplicarPaginacao();
            StateHasChanged();
        }
    }

    private void ProximaPagina()
    {
        if (paginaAtual < totalPaginas)
        {
            IrParaPagina(paginaAtual + 1);
        }
    }

    private void PaginaAnterior()
    {
        if (paginaAtual > 1)
        {
            IrParaPagina(paginaAtual - 1);
        }
    }

    private List<int> ObterPaginasParaExibir()
    {
        var paginas = new List<int>();
        var inicio = Math.Max(1, paginaAtual - 2);
        var fim = Math.Min(totalPaginas, paginaAtual + 2);

        for (int i = inicio; i <= fim; i++)
        {
            paginas.Add(i);
        }

        return paginas;
    }

    private string GetStatusClass(StatusJob status)
    {
        return status switch
        {
            StatusJob.Pendente => "badge bg-warning",
            StatusJob.Processando => "badge bg-info",
            StatusJob.Concluido => "badge bg-success",
            StatusJob.Erro => "badge bg-danger",
            _ => "badge bg-secondary"
        };
    }

    private string GetTipoClass(TipoJob tipo)
    {
        return tipo switch
        {
            TipoJob.Produtos => "badge bg-primary",
            TipoJob.Plotter => "badge bg-secondary",
            TipoJob.CarePack => "badge bg-info",
            TipoJob.Promocao => "badge bg-warning",
            TipoJob.Base => "badge bg-success",
            _ => "badge bg-secondary"
        };
    }

    private async Task LimparTodosJobs()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            var result = await _jobService.LimparTodosJobs();
            
            if (result.jobsCancelados > 0)
            {
                mensagem = $"✅ {result.message} ({result.jobsCancelados} job(s) em execução foi(foram) cancelado(s))";
            }
            else
            {
                mensagem = $"✅ {result.message}";
            }
            IniciarTimerMensagem();
            
            // Recarregar a lista de jobs
            await CarregarJobs();
        }
        catch (Exception ex)
        {
            mensagem = $"❌ Erro ao limpar jobs: {ex.Message}";
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private void AplicarFiltros()
    {
        jobsFiltrados = todosJobs.ToList();
        
        // Aplicar filtro por status
        if (filtroStatus.HasValue)
        {
            jobsFiltrados = jobsFiltrados.Where(j => j.Status == filtroStatus.Value).ToList();
        }
    }

    private void FiltrarPorStatus(StatusJob status)
    {
        // Se clicar no mesmo status, remove o filtro
        if (filtroStatus == status)
        {
            filtroStatus = null;
        }
        else
        {
            filtroStatus = status;
        }
        
        // Volta para a primeira página
        paginaAtual = 1;
        
        // Aplica os filtros e paginação
        AplicarPaginacao();
        StateHasChanged();
    }



    private void LimparFiltros()
    {
        filtroStatus = null;
        paginaAtual = 1;
        AplicarPaginacao();
        StateHasChanged();
    }

    /// <summary>
    /// Extrai o nome do arquivo de um caminho completo de forma segura
    /// </summary>
    /// <param name="caminho">Caminho completo do arquivo</param>
    /// <returns>Nome do arquivo ou "Arquivo não disponível" se o caminho for nulo/vazio</returns>
    private string GetFileName(string? caminho)
    {
        if (string.IsNullOrEmpty(caminho))
            return "Arquivo não disponível";
        
        try
        {
            return Path.GetFileName(caminho);
        }
        catch
        {
            // Se houver erro ao extrair o nome, retorna o caminho original ou uma mensagem padrão
            return !string.IsNullOrEmpty(caminho) ? caminho : "Arquivo não disponível";
        }
    }

    public void Dispose()
    {
        CancelarTimerMensagem();
    }
}