using System.Net.Http.Json;
using eCommerce.Shared.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Text.Json;

namespace eCommerce.Client.Services.Bling;

public class BlingService 
{
    
    private readonly HttpClient _http;
    private readonly IJSRuntime _jsRuntime;
    private TokenResponse? _currentToken;

    public BlingService(HttpClient http, IJSRuntime jsRuntime)
    {
        _http = http;
        _jsRuntime = jsRuntime;
    }

    public TokenResponse? CurrentToken => _currentToken;

    public async Task<string> ObterCodigo()
    {
       var url = "https://www.bling.com.br/Api/v3/oauth/authorize?response_type=code&client_id=ec6a23e8433ec9f24b2b7787bf4f5deea2892064&state=53a7fb14d9f453aa0dfd8eb1322a65a1";
       var response = await _http.GetAsync(url);
       if (response.IsSuccessStatusCode)
       {
            return await response.Content.ReadAsStringAsync();
        }
        else
        {
            throw new HttpRequestException($"Erro ao obter código: {response.StatusCode}");
        }
    }

    public async Task<string> RedirecionarParaAutorizacao()
    {
        var redirectUri = "http://127.0.0.1:5009/bling-callback.html";
        var url = $"https://www.bling.com.br/Api/v3/oauth/authorize?response_type=code&client_id=ec6a23e8433ec9f24b2b7787bf4f5deea2892064&state=53a7fb14d9f453aa0dfd8eb1322a65a1&redirect_uri={Uri.EscapeDataString(redirectUri)}";
        
        // Abrir popup para autorização
        await _jsRuntime.InvokeVoidAsync("window.open", url, "blingAuth", "width=600,height=700,scrollbars=yes,resizable=yes");
        
        // Aguardar o código ser capturado via JavaScript
        return await _jsRuntime.InvokeAsync<string>("waitForBlingAuth");
    }

    public async Task<string> CapturarCodigoDaPopup()
    {
        return await _jsRuntime.InvokeAsync<string>("captureBlingCode");
    }

    public string? ExtrairCodigoDaUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var query = uri.Query;
            
            if (string.IsNullOrEmpty(query))
                return null;
                
            // Remove o '?' inicial
            query = query.TrimStart('?');
            
            // Divide os parâmetros
            var parametros = query.Split('&');
            
            foreach (var parametro in parametros)
            {
                var partes = parametro.Split('=');
                if (partes.Length == 2 && partes[0] == "code")
                {
                    return partes[1];
                }
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<TokenResponse> TrocarCodigoPorToken(string codigo)
    {
        try
        {
            // Fazer requisição através do servidor backend
            var request = new
            {
                code = codigo,
                grant_type = "authorization_code"
            };

            var response = await _http.PostAsJsonAsync("api/bling/token", request);
            
            if (response.IsSuccessStatusCode)
            {
                var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
                _currentToken = tokenResponse ?? throw new HttpRequestException("Falha ao deserializar resposta do token");
                return tokenResponse;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Erro ao trocar código por token: {response.StatusCode} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            throw new HttpRequestException($"Erro na requisição: {ex.Message}");
        }
    }

    public async Task<TokenResponse> ObterToken()
    {
        try
        {
            var response = await _http.GetAsync("api/bling/token");
            
            if (response.IsSuccessStatusCode)
            {
                var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
                _currentToken = tokenResponse ?? throw new HttpRequestException("Falha ao deserializar resposta do token");
                return tokenResponse;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException("Nenhum token encontrado. Execute a autorização OAuth primeiro.");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Token expirado ou inválido: {errorContent}");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Erro ao obter token: {response.StatusCode} - {errorContent}");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new HttpRequestException($"Erro ao obter token: {ex.Message}");
        }
    }

    public async Task<bool> VerificarTokenValido()
    {
        try
        {
            var token = await ObterToken();
            return token != null && !string.IsNullOrEmpty(token.access_token);
        }
        catch
        {
            return false;
        }
    }

    public async Task<TokenResponse> RenovarToken()
    {
        try
        {
            var response = await _http.PostAsync("api/bling/refresh-token", null);
            
            if (response.IsSuccessStatusCode)
            {
                var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
                _currentToken = tokenResponse ?? throw new HttpRequestException("Falha ao deserializar resposta do token");
                return tokenResponse;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Erro ao renovar token: {response.StatusCode} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            throw new HttpRequestException($"Erro ao renovar token: {ex.Message}");
        }
    }

    public async Task<string> FazerRequisicaoAutenticada(string endpoint)
    {
        try
        {
            // Tentar obter token se não tiver um
            if (_currentToken == null || string.IsNullOrEmpty(_currentToken.access_token))
            {
                try
                {
                    await ObterToken();
                }
                catch
                {
                    throw new InvalidOperationException("Token de acesso não disponível. Execute a autorização OAuth primeiro.");
                }
            }

            var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.bling.com.br/Api/v3/{endpoint}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _currentToken!.access_token);

            var response = await _http.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Token pode ter expirado, tentar renovar
                try
                {
                    await RenovarToken();
                    
                    // Tentar novamente com o novo token
                    request = new HttpRequestMessage(HttpMethod.Get, $"https://api.bling.com.br/Api/v3/{endpoint}");
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _currentToken!.access_token);
                    
                    response = await _http.SendAsync(request);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStringAsync();
                    }
                }
                catch
                {
                    throw new InvalidOperationException("Token expirado e não foi possível renovar. Execute a autorização OAuth novamente.");
                }
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Erro na requisição autenticada: {response.StatusCode} - {errorContent}");
        }
        catch (Exception ex) when (ex is not HttpRequestException && ex is not InvalidOperationException)
        {
            throw new HttpRequestException($"Erro na requisição autenticada: {ex.Message}");
        }
    }

    public async Task<JobFilaResponse> EnviarProdutos(string loja)
    {
        var request = new { loja = loja };
        var response = await _http.PostAsJsonAsync("api/bling/produtos", request);
        if (response.IsSuccessStatusCode)
        {
            var job = await response.Content.ReadFromJsonAsync<JobFilaResponse>();
            return job ?? throw new HttpRequestException("Resposta vazia do servidor");
        }
        else
        {
            throw new HttpRequestException($"Erro ao enviar produtos: {response.StatusCode}");
        }
    }
}