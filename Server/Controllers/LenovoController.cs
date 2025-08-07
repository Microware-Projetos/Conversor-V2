using Microsoft.AspNetCore.Mvc;
using LiteDB;
using eCommerce.Shared.Models;
using eCommerce.Server.Services.Lenovo;
using eCommerce.Server.Helpers;

namespace eCommerce.Server.Controllers;

[ApiController]
[Route("api/lenovo")]
public class LenovoController : ControllerBase
{
    private readonly LiteDatabase _db;
    private readonly LenovoService _lenovoService;

    public LenovoController(LiteDatabase db, LenovoService lenovoService)
    {
        _db = db;
        _lenovoService = lenovoService;
    }

    [HttpPost("produtos")]
    public async Task<IActionResult> EnviarProdutos([FromForm] IFormFile arquivoProdutos)
    {
        var pastaUploads = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        var caminhoProdutos = FileHelper.SaveFile(arquivoProdutos, pastaUploads);

        var job = await _lenovoService.EnviarProdutos(arquivoProdutos);
        return Ok(job);
    }

    [HttpPost("carepack")]
    public async Task<IActionResult> EnviarCarePack([FromForm] IFormFile arquivoProdutos)
    {
        var pastaUploads = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
        var caminhoProdutos = FileHelper.SaveFile(arquivoProdutos, pastaUploads);

        var job = await _lenovoService.EnviarCarePack(arquivoProdutos);
        return Ok(job);
    }
}