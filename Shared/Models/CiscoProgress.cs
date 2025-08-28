namespace eCommerce.Shared.Models;

public class CiscoProgress
{
    public int LoteAtual { get; set; }
    public int Total { get; set; }
    public string Status { get; set; } = "Aguardando...";
    public int Erros { get; set; }
    public int Sucessos { get; set; }
}