using Microsoft.JSInterop;
using eCommerce.Client.Services.Bling;
using eCommerce.Shared.Models;
using System.Threading;

namespace eCommerce.Client.Pages.Bling;

partial class Bling
{
    private string? mensagem;
    private TokenResponse? tokenResponse;
    private bool isLoading = false;
    private string loadingMessage = "";

    protected override async Task OnInitializedAsync()
    {
        await CarregarTokenExistente();
    }

    private async Task CarregarTokenExistente()
    {
        try
        {
            var tokenValido = await BlingService.VerificarTokenValido();
            if (tokenValido)
            {
                tokenResponse = await BlingService.ObterToken();
                mensagem = "Token válido encontrado e carregado.";
                StateHasChanged();
            }
        }
        catch
        {
            // Token não encontrado ou inválido, não mostrar erro
        }
    }

    private async Task EnviarProdutosAutomaticamente()
    {
        isLoading = true;
        loadingMessage = "Verificando autenticação...";
        StateHasChanged();

        try
        {
            // Passo 1: Verificar se já existe um token válido
            mensagem = "Verificando token existente...";
            StateHasChanged();

            var tokenValido = await BlingService.VerificarTokenValido();
            if (tokenValido)
            {
                try
                {
                    tokenResponse = await BlingService.ObterToken();
                    mensagem = "Token válido encontrado! Iniciando envio de produtos...";
                    StateHasChanged();
                    
                    // Token válido, prosseguir com envio
                    await EnviarProdutos();
                    return;
                }
                catch (Exception ex)
                {
                    mensagem = $"Token inválido: {ex.Message}. Iniciando nova autorização...";
                    StateHasChanged();
                }
            }

            // Passo 2: Se não há token válido, iniciar processo de autorização
            loadingMessage = "Iniciando autorização...";
            StateHasChanged();
            
            mensagem = "Iniciando autorização OAuth...";
            StateHasChanged();

            var codigoAutorizacao = await BlingService.RedirecionarParaAutorizacao();
            
            if (string.IsNullOrEmpty(codigoAutorizacao))
            {
                throw new InvalidOperationException("Não foi possível obter o código de autorização. Verifique se a popup foi fechada corretamente.");
            }

            // Passo 3: Trocar código por token
            loadingMessage = "Obtendo token de acesso...";
            StateHasChanged();
            
            mensagem = "Obtendo token de acesso...";
            StateHasChanged();

            tokenResponse = await BlingService.TrocarCodigoPorToken(codigoAutorizacao);
            
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.access_token))
            {
                throw new InvalidOperationException("Falha ao obter token de acesso.");
            }

            // Passo 4: Enviar produtos
            loadingMessage = "Enviando produtos...";
            StateHasChanged();
            
            mensagem = "Token obtido com sucesso! Enviando produtos...";
            StateHasChanged();

            await EnviarProdutos();
        }
        catch (TaskCanceledException)
        {
            mensagem = "Timeout na operação. Verifique sua conexão com a internet.";
        }
        catch (Exception ex)
        {
            mensagem = $"Erro: {ex.Message}";
        }
        finally
        {
            isLoading = false;
            loadingMessage = "";
            StateHasChanged();
        }
    }

    private async Task EnviarProdutos()
    {
        try
        {
            var loja = "HP";
            var job = await BlingService.EnviarProdutos(loja);
            mensagem = $"✅ Produtos enviados com sucesso! Job ID: {job.Id}";
            StateHasChanged();
        }
        catch (Exception ex)
        {
            mensagem = $"❌ Erro ao enviar produtos: {ex.Message}";
            StateHasChanged();
        }
    }

    private async Task GerarNovoToken()
    {
        isLoading = true;
        loadingMessage = "Descartando token atual...";
        StateHasChanged();

        var sucesso = await BlingService.DeletarToken();
        if (sucesso)
        {
            tokenResponse = null;
            mensagem = "Token anterior removido. Iniciando nova autorização...";
            StateHasChanged();

            try
            {
                var codigoAutorizacao = await BlingService.RedirecionarParaAutorizacao();
                if (string.IsNullOrEmpty(codigoAutorizacao))
                {
                    throw new InvalidOperationException("Não foi possível obter o código de autorização. Verifique se a popup foi fechada corretamente.");
                }
                loadingMessage = "Obtendo novo token de acesso...";
                StateHasChanged();
                tokenResponse = await BlingService.TrocarCodigoPorToken(codigoAutorizacao);
                if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.access_token))
                {
                    throw new InvalidOperationException("Falha ao obter novo token de acesso.");
                }
                mensagem = "Novo token obtido com sucesso!";
            }
            catch (Exception ex)
            {
                mensagem = $"Erro ao gerar novo token: {ex.Message}";
            }
        }
        else
        {
            mensagem = "Erro ao remover o token atual.";
        }
        isLoading = false;
        loadingMessage = string.Empty;
        StateHasChanged();
    }
}