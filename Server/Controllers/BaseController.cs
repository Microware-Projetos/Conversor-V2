using Microsoft.AspNetCore.Mvc;
using LiteDB;
using eCommerce.Shared.Models;
using eCommerce.Server.Services.Base;

namespace eCommerce.Server.Controllers;

[ApiController]
[Route("api/Base")]
public class BaseController : ControllerBase
{
    private readonly LiteDatabase _db;
    private readonly BaseService _baseService;
    private readonly HttpClient _httpClient;

    public BaseController(LiteDatabase db, BaseService baseService, HttpClient httpClient)
    {
        _db = db;
        _baseService = baseService;
        _httpClient = httpClient;
    }
    
    [HttpPost("produtos")]
    public async Task<IActionResult> EnviarProdutos([FromBody] EnviarProdutosRequest request)
    {
        var job = await _baseService.EnviarProdutos(request.loja);
        return Ok(job);
    }

    [HttpPost("token")]
    public async Task<IActionResult> TrocarCodigoPorToken([FromBody] TokenRequest request)
    {
        try
        {
            Console.WriteLine($"Iniciando troca de código por token. Code: {request.code}");
            
            var url = "https://api.Base.com.br/Api/v3/oauth/token";
            
            // Configurar timeout de 30 segundos
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            // Configurar autenticação Basic
            var clientId = "ec6a23e8433ec9f24b2b7787bf4f5deea2892064";
            var clientSecret = "ac6c2469183b640a4553da7a541bb2f2f5235d056a301e7e51cc3fef4beb";
            var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
            
            // Preparar os dados do formulário
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", request.grant_type),
                new KeyValuePair<string, string>("code", request.code)
            });

            Console.WriteLine($"Fazendo requisição para: {url}");
            var response = await httpClient.PostAsync(url, formData);
            
            Console.WriteLine($"Resposta recebida: {response.StatusCode}");
            
            if (response.IsSuccessStatusCode)
            {
                var tokenResponseJson = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Token response: {tokenResponseJson}");
                var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(tokenResponseJson);
                
                // Salvar token no banco de dados
                if (tokenResponse != null)
                {
                    var tokenCollection = _db.GetCollection<BaseToken>("Base_tokens");
                    
                    // Remover tokens antigos
                    tokenCollection.DeleteAll();
                    
                    // Salvar novo token
                    var BaseToken = new BaseToken
                    {
                        Id = ObjectId.NewObjectId(),
                        AccessToken = tokenResponse.access_token,
                        TokenType = tokenResponse.token_type,
                        ExpiresIn = tokenResponse.expires_in,
                        RefreshToken = tokenResponse.refresh_token,
                        Scope = tokenResponse.scope,
                        DataCriacao = DateTime.Now,
                        DataExpiracao = DateTime.Now.AddSeconds(tokenResponse.expires_in ?? 0)
                    };
                    
                    tokenCollection.Insert(BaseToken);
                    Console.WriteLine($"Token salvo no banco com ID: {BaseToken.Id}");
                }
                
                return Ok(tokenResponse);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Erro na resposta: {errorContent}");
                return BadRequest($"Erro ao trocar código por token: {response.StatusCode} - {errorContent}");
            }
        }
        catch (TaskCanceledException ex)
        {
            Console.WriteLine($"Timeout na requisição: {ex.Message}");
            return BadRequest("Timeout na requisição. Verifique sua conexão com a internet.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro na requisição: {ex.Message}");
            return BadRequest($"Erro na requisição: {ex.Message}");
        }
    }

    [HttpGet("token")]
    public IActionResult ObterToken()
    {
        try
        {
            var tokenCollection = _db.GetCollection<BaseToken>("Base_tokens");
            var token = tokenCollection.Query().FirstOrDefault();
            
            if (token == null)
            {
                return NotFound("Nenhum token encontrado. Execute a autorização OAuth primeiro.");
            }
            
            if (token.IsExpired)
            {
                return BadRequest("Token expirado. Execute a autorização OAuth novamente.");
            }
            
            return Ok(new TokenResponse
            {
                access_token = token.AccessToken,
                token_type = token.TokenType,
                expires_in = token.ExpiresIn,
                refresh_token = token.RefreshToken,
                scope = token.Scope
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao obter token: {ex.Message}");
            return BadRequest($"Erro ao obter token: {ex.Message}");
        }
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RenovarToken()
    {
        try
        {
            var tokenCollection = _db.GetCollection<BaseToken>("Base_tokens");
            var currentToken = tokenCollection.Query().FirstOrDefault();
            
            if (currentToken == null || string.IsNullOrEmpty(currentToken.RefreshToken))
            {
                return BadRequest("Nenhum refresh token disponível. Execute a autorização OAuth novamente.");
            }
            
            var url = "https://api.Base.com.br/Api/v3/oauth/token";
            
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            // Configurar autenticação Basic
            var clientId = "ec6a23e8433ec9f24b2b7787bf4f5deea2892064";
            var clientSecret = "ac6c2469183b640a4553da7a541bb2f2f5235d056a301e7e51cc3fef4beb";
            var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
            
            // Preparar dados para renovação
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", currentToken.RefreshToken)
            });
            
            Console.WriteLine("Renovando token...");
            var response = await httpClient.PostAsync(url, formData);
            
            if (response.IsSuccessStatusCode)
            {
                var tokenResponseJson = await response.Content.ReadAsStringAsync();
                var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(tokenResponseJson);
                
                if (tokenResponse != null)
                {
                    // Atualizar token no banco
                    currentToken.AccessToken = tokenResponse.access_token;
                    currentToken.TokenType = tokenResponse.token_type;
                    currentToken.ExpiresIn = tokenResponse.expires_in;
                    currentToken.RefreshToken = tokenResponse.refresh_token ?? currentToken.RefreshToken; // Manter o anterior se não vier novo
                    currentToken.Scope = tokenResponse.scope;
                    currentToken.DataCriacao = DateTime.Now;
                    currentToken.DataExpiracao = DateTime.Now.AddSeconds(tokenResponse.expires_in ?? 0);
                    
                    tokenCollection.Update(currentToken);
                    Console.WriteLine($"Token renovado com sucesso. Novo ID: {currentToken.Id}");
                    
                    return Ok(tokenResponse);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Erro ao renovar token: {errorContent}");
                return BadRequest($"Erro ao renovar token: {response.StatusCode} - {errorContent}");
            }
            
            return BadRequest("Falha ao renovar token");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao renovar token: {ex.Message}");
            return BadRequest($"Erro ao renovar token: {ex.Message}");
        }
    }

    [HttpDelete("token")]
    public IActionResult DeletarToken()
    {
        try
        {
            var tokenCollection = _db.GetCollection<BaseToken>("Base_tokens");
            tokenCollection.DeleteAll();
            return Ok("Token removido com sucesso.");
        }
        catch (Exception ex)
        {
            return BadRequest($"Erro ao remover token: {ex.Message}");
        }
    }
} 