using Microsoft.AspNetCore.Mvc;
using LiteDB;
using eCommerce.Shared.Models;
using eCommerce.Server.Services.HP;

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
        var job = await _hpService.EnviarProdutos(arquivoProdutos, arquivoPrecos);
        return Ok(job);
    }

}