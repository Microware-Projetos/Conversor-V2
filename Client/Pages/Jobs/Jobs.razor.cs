using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using eCommerce.Client.Services.Job;
using eCommerce.Shared.Models;

namespace eCommerce.Client.Pages.Jobs;

public partial class Jobs : ComponentBase
{
    [Inject]
    private JobService _jobService { get; set; } = default!;

    private List<JobFilaResponse> todosJobs = new List<JobFilaResponse>();
    private List<JobFilaResponse> jobsExibidos = new List<JobFilaResponse>();
    private bool isLoading = false;
    private string mensagem = "";
    private const int ITENS_POR_PAGINA = 10;
    private int paginaAtual = 1;
    private int totalPaginas = 1;

    protected override async Task OnInitializedAsync()
    {
        await CarregarJobs();
    }

    private void LimparMensagem()
    {
        mensagem = "";
        StateHasChanged();
    }

    private async Task CarregarJobs()
    {
        try
        {
            isLoading = true;
            StateHasChanged();
            
            todosJobs = await _jobService.ListarJobs();
            CalcularPaginas();
            AplicarPaginacao();
            mensagem = $"Carregados {todosJobs.Count} jobs";
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
        totalPaginas = (int)Math.Ceiling((double)todosJobs.Count / ITENS_POR_PAGINA);
        if (totalPaginas == 0) totalPaginas = 1;
        if (paginaAtual > totalPaginas) paginaAtual = totalPaginas;
    }

    private void AplicarPaginacao()
    {
        var jobsOrdenados = todosJobs.OrderByDescending(j => j.DataCriacao).ToList();
        var inicio = (paginaAtual - 1) * ITENS_POR_PAGINA;
        jobsExibidos = jobsOrdenados.Skip(inicio).Take(ITENS_POR_PAGINA).ToList();
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
            TipoJob.Bling => "badge bg-success",
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
}