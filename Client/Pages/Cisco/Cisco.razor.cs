using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using eCommerce.Client.Services.Cisco;
using eCommerce.Shared.Models;
using System.Globalization;

namespace eCommerce.Client.Pages.Cisco;

public partial class Cisco : ComponentBase
{
    [Inject]
    private CiscoService _ciscoService { get; set; } = default!;

    private string tipoUpload = "ListaReal";
    private IBrowserFile? arquivoProdutos;
    private string mensagem = "";
    private bool isLoading = false;
    private double valorDolar = 0;
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
        if (string.IsNullOrEmpty(tipoUpload))
        {
            mensagem = "Selecione o tipo de upload";
            return;
        }

        if (arquivoProdutos == null)
        {
            mensagem = "Selecione um arquivo";
            return;
        }

        if (valorDolar <= 0 && tipoUpload == "ListaDolar")
        {
            mensagem = "O valor do dólar deve ser maior que zero";
            return;
        }

        isLoading = true;
        mensagem = "";

        try
        {
            if (tipoUpload == "ListaReal")
            {
                _ciscoService.EnviarListaReal(arquivoProdutos);
                mensagem = "Lista Real enviada com sucesso!";
            }
            else if (tipoUpload == "ListaDolar")
            {
                _ciscoService.EnviarListaDolar(arquivoProdutos, valorDolar);
                mensagem = "Lista Dólar enviada com sucesso!";
            }
            
            arquivoProdutos = null;
        }
        catch (Exception ex)
        {
            mensagem = $"Erro ao enviar arquivo: {ex.Message}";
        }
        finally
        {
            isLoading = false;
        }
    }

    private void NavigateToReal()
    {
        Navigation.NavigateTo("/cisco/revisao/real");
    }

    private void NavigateToDolar()
    {
        Navigation.NavigateTo("/cisco/revisao/dolar");
    }
}