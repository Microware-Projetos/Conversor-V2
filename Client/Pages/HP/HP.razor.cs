using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using eCommerce.Client.Services.HP;
using eCommerce.Shared.Models;

namespace eCommerce.Client.Pages.HP;

public partial class HP : ComponentBase
{
    [Inject]
    private HPService _hpService { get; set; } = default!;

    private string tipoUpload = "Produtos";
    private IBrowserFile? arquivoProdutos;
    private IBrowserFile? arquivoPrecos;
    private string mensagem = "";
    private bool isLoading = false;
    private const long MAX_FILE_SIZE = 50 * 1024 * 1024; // 50MB

    private void LimparMensagem()
    {
        mensagem = "";
        StateHasChanged();
    }

    private void OnProdutosFileChange(InputFileChangeEventArgs e)
    {
        arquivoProdutos = e.File;
        ValidarArquivo(arquivoProdutos, "Produtos");
    }

    private void OnPrecosFileChange(InputFileChangeEventArgs e)
    {
        arquivoPrecos = e.File;
        ValidarArquivo(arquivoPrecos, "Preços");
    }

    private void ValidarArquivo(IBrowserFile? arquivo, string nomeArquivo)
    {
        if (arquivo == null) return;

        if (arquivo.Size > MAX_FILE_SIZE)
        {
            mensagem = $"Erro: O arquivo {nomeArquivo} ({arquivo.Size / 1024 / 1024}MB) excede o limite de {MAX_FILE_SIZE / 1024 / 1024}MB";
            StateHasChanged();
        }
        else
        {
            mensagem = $"Arquivo {nomeArquivo} carregado: {arquivo.Name} ({arquivo.Size / 1024}KB)";
            StateHasChanged();
        }
    }

    private async Task EnviarParaWooCommerce()
    {
        if (tipoUpload == "Produtos")
        {
            if (arquivoProdutos == null || arquivoPrecos == null)
            {
                mensagem = "Por favor, selecione ambos os arquivos antes de enviar.";
                StateHasChanged();
                return;
            }

            if (arquivoProdutos.Size > MAX_FILE_SIZE || arquivoPrecos.Size > MAX_FILE_SIZE)
            {
                mensagem = "Um ou ambos os arquivos excedem o tamanho máximo permitido.";
                StateHasChanged();
                return;
            }
        }

        if (tipoUpload != "Produtos" && arquivoProdutos == null)
        {
            mensagem = "Por favor, selecione o arquivo antes de enviar.";
            StateHasChanged();
            return;
        }

        if (tipoUpload != "Produtos" && arquivoProdutos.Size > MAX_FILE_SIZE)
        {
            mensagem = "O arquivo excede o tamanho máximo permitido.";
            StateHasChanged();
            return;
        }

        isLoading = true;
        mensagem = "Enviando arquivos...";
        StateHasChanged();

        try
        {
            switch (tipoUpload)
            {
                case "Produtos":
                    await EnviarProdutos();
                    break;
                case "Plotter":
                    await EnviarPlotter();
                    break;
                case "Care Pack":
                    await EnviarCarePack();
                    break;
                case "Promoção":
                    await EnviarPromocao();
                    break;
            }
        }
        catch (Exception ex)
        {
            mensagem = $"Erro ao enviar arquivos: {ex.Message}";
            StateHasChanged();
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task EnviarProdutos()
    {
        Console.WriteLine("Enviando Produtos");
        var job = await _hpService.EnviarProdutos(arquivoProdutos!, arquivoPrecos!);
        mensagem = $"Produtos enviados com sucesso! Job ID: {job.Id}";
        StateHasChanged();
    }

    private async Task EnviarPlotter()
    {
        Console.WriteLine("Enviando Plotter");
        var job = await _hpService.EnviarPlotter(arquivoProdutos!);
        mensagem = $"Plotters enviados com sucesso! Job ID: {job.Id}";
        StateHasChanged();
    }

    private async Task EnviarCarePack()
    {
        Console.WriteLine("Enviando Care Pack");
        var job = await _hpService.EnviarCarePack(arquivoProdutos!);
        mensagem = $"Care Packs enviados com sucesso! Job ID: {job.Id}";
        StateHasChanged();
    }

    private async Task EnviarPromocao()
    {
        Console.WriteLine("Enviando Promoção");
        var job = await _hpService.EnviarPromocao(arquivoProdutos!);
        mensagem = $"Promoções enviadas com sucesso! Job ID: {job.Id}";
        StateHasChanged();
    }
}