using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using eCommerce.Client.Services.Lenovo;

namespace eCommerce.Client.Pages.Lenovo;

public partial class Lenovo : ComponentBase
{
    [Inject]
    private LenovoService _lenovoService { get; set; } = default!;

    private string tipoUpload = "Produtos";
    private IBrowserFile? arquivoProdutos;
    private string mensagem = "";
    private bool isLoading = false;
    private const long MAX_FILE_SIZE = 50 * 1024 * 1024; // 50MB - Verificar se o limite é o mesmo
    
    private void LimparMensagem()
    {
        mensagem = "";
        StateHasChanged();
    }


    // Recebe o arquivo de produtos e chama a função para validá-lo
    private void OnProdutosFileChange(InputFileChangeEventArgs e)
    {
        arquivoProdutos = e.File;
        ValidarArquivo(arquivoProdutos, "Produtos");
    }

    // Recebe o arquivo e o seu nome e valida o tamanho do arquivo
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
        if (arquivoProdutos == null)
        {
            mensagem = "Por favor, selecione  o arquivo antes de enviar.";
            StateHasChanged();
            return;
        }

        if (arquivoProdutos.Size > MAX_FILE_SIZE)
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
                case "Care Pack":
                    await EnviarCarePack();
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
        var job = await _lenovoService.EnviarProdutos(arquivoProdutos!);
        mensagem = $"Produtos enviados com sucesso! Job ID: {job.Id}";
        StateHasChanged();
    }
    private async Task EnviarCarePack()
    {
        // TODO: Implementar chamada de API para Care Pack usando arquivoProdutos
        Console.WriteLine("Enviando Care Pack");
        var job = await _lenovoService.EnviarCarePack(arquivoProdutos!);
        mensagem = $"Produtos enviados com sucesso! Job ID: {job.Id}";
        StateHasChanged();
    }


}