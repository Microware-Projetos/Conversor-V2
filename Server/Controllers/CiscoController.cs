using Microsoft.AspNetCore.Mvc;
using LiteDB;
using eCommerce.Shared.Models;
using eCommerce.Server.Services.Cisco;
using eCommerce.Server.Helpers;
using System.Text.Json;

namespace eCommerce.Server.Controllers;

[ApiController]
[Route("api/cisco")]
public class CiscoController : ControllerBase
{
    private readonly LiteDatabase _db;
    private readonly CiscoService _ciscoService;

    public CiscoController(LiteDatabase db, CiscoService ciscoService)
    {
        _db = db;
        _ciscoService = ciscoService;
    }

    
    [HttpPost("real")]
    public async Task<IActionResult> EnviarListaReal([FromForm] IFormFile arquivoProdutos)
    {
        var pastaUploads = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");

        var job = await _ciscoService.EnviarListaReal(arquivoProdutos);
        return Ok(job);
    }

    [HttpPost("lista-dolar")]
    public async Task<IActionResult> EnviarListaDolar([FromForm] IFormFile arquivoProdutos, [FromForm] double valorDolar)
    {
        var pastaUploads = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");

        var job = await _ciscoService.EnviarListaDolar(arquivoProdutos, valorDolar);
        return Ok(job);
    }

    [HttpGet("dolar")]
    public async Task<IActionResult> GetDolar()
    {
        var dolar = CiscoService.GetDolarValue();
        return Ok(dolar);
    }

    [HttpPost("dolar")]
    public async Task<IActionResult> UpdateDolar([FromForm] double valor)
    {
        try
        {
            if (valor <= 0)
                return BadRequest(new { erro = "Valor do dolar inválido" });

            _ciscoService.SaveDolarValue(valor);
            return Ok(new { mensagem = "Valor do dolar atualizado com sucesso" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { erro = $"Erro ao atualizar valor do dólar: {ex.Message}" });
        }
    }

    [HttpGet("progresso")]
    public async Task GetProgresso()
    {
        Response.Headers.Add("Cache-Control", "no-cache");
        Response.Headers.Add("Content-Type", "text/event-stream");
        Response.Headers.Add("Connection", "keep-alive");

        var responseStream = Response.Body;

        while (true)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(CiscoStateProgress.Atual);

            var message = $"data: {json}\n\n";
            var bytes = System.Text.Encoding.UTF8.GetBytes(message);

            await responseStream.WriteAsync(bytes, 0, bytes.Length);
            await responseStream.FlushAsync();

            await Task.Delay(1000);
        }
    }

    [HttpPost("progresso")]
    public async Task<IActionResult> UpdateProgresso([FromForm] CiscoProgress progresso)
    {
        CiscoStateProgress.Atual = progresso;
        return Ok(new { mensagem = "Progresso atualizado com sucesso" });
    }

    [HttpPost("deletar")]
    public async Task<IActionResult> DeleteAllProducts()
    {
        try
        {
            System.Console.WriteLine("Iniciando deleção de todos os produtos Cisco...");
            await _ciscoService.DeletarTodosProdutosCisco();
            return Ok(new { mensagem = "Todos os produtos Cisco foram deletados com sucesso!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { erro = $"Erro ao deletar produtos: {ex.Message}" });
        }
    }

    [HttpPost("revisao/real/enviar")]
    public async Task<IActionResult> EnviarListaRealRevisao([FromForm] IFormFile arquivoProdutos)
    {
        var job = await _ciscoService.EnviarListaRealRevisao(arquivoProdutos);
        return Ok(job);
    }
}