using Microsoft.AspNetCore.Mvc;
using LiteDB;
using eCommerce.Shared.Models;
using eCommerce.Server.Services.HP;
using eCommerce.Server.Helpers;

namespace eCommerce.Server.Controllers;

[ApiController]
[Route("api/hp")]
public class HPController : ControllerBase
{
    private readonly LiteDatabase _db;
    private readonly HPService _hpService;

    public HPController(LiteDatabase db, HPService hpService)
    {
        _db = db;
        _hpService = hpService;
    }
    
    [HttpPost("produtos")]
    public async Task<IActionResult> EnviarProdutos([FromForm] IFormFile arquivoProdutos, [FromForm] IFormFile arquivoPrecos)
    {
        var pastaUploads = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        var caminhoProdutos = FileHelper.SaveFile(arquivoProdutos, pastaUploads);
        var caminhoPrecos = FileHelper.SaveFile(arquivoPrecos, pastaUploads);

        var job = await _hpService.EnviarProdutos(arquivoProdutos, arquivoPrecos);
        return Ok(job);
    }

    [HttpPost("carepack")]
    public async Task<IActionResult> EnviarCarePack([FromForm] IFormFile arquivoProdutos)
    {
        var pastaUploads = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        var caminhoProdutos = FileHelper.SaveFile(arquivoProdutos, pastaUploads);
        
        var job = await _hpService.EnviarCarePack(arquivoProdutos);
        return Ok(job);
    }

    [HttpPost("plotter")]
    public async Task<IActionResult> EnviarPlotter([FromForm] IFormFile arquivoProdutos)
    {
        var pastaUploads = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        var caminhoProdutos = FileHelper.SaveFile(arquivoProdutos, pastaUploads);
        
        var job = await _hpService.EnviarPlotter(arquivoProdutos);
        return Ok(job);
    }

}