using LiteDB;

namespace eCommerce.Shared.Models;

public class TokenRequest
{
    public string code { get; set; } = "";
    public string grant_type { get; set; } = "";
}

public class EnviarProdutosRequest
{
    public string loja { get; set; } = "";
}

public class TokenResponse
{
    public string? access_token { get; set; }
    public string? token_type { get; set; }
    public int? expires_in { get; set; }
    public string? refresh_token { get; set; }
    public string? scope { get; set; }
}

public class BaseToken
{
    public ObjectId Id { get; set; }
    public string? AccessToken { get; set; }
    public string? TokenType { get; set; }
    public int? ExpiresIn { get; set; }
    public string? RefreshToken { get; set; }
    public string? Scope { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime DataExpiracao { get; set; }
    
    public bool IsExpired => DateTime.Now >= DataExpiracao;
    public bool IsExpiringSoon => DateTime.Now >= DataExpiracao.AddMinutes(-5); // 5 minutos antes de expirar
} 