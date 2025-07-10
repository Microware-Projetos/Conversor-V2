using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace eCommerce.Client.Pages.HP;

public partial class HP : ComponentBase
{
    
    private string tipoUpload = "Produtos";
    private IBrowserFile? arquivoProdutos;
    private IBrowserFile? arquivoPrecos;

    private void OnProdutosFileChange(InputFileChangeEventArgs e)
    {
        arquivoProdutos = e.File;
    }

    private void OnPrecosFileChange(InputFileChangeEventArgs e)
    {
        arquivoPrecos = e.File;
    }

    private async Task EnviarParaWooCommerce()
    {
        switch (tipoUpload)
        {
            case "Produtos":
                await EnviarProdutos();
                break;
            case "Plotter":
                await EnviarPlotter();
                break;
            case "Care Pack":
                await EnviarCarePack();
                break;
            case "Promoção":
                await EnviarPromocao();
                break;
        }
    }

    private Task EnviarProdutos()
    {
        // TODO: Implementar chamada de API para Produtos usando arquivoProdutos e arquivoPrecos
        Console.WriteLine("Enviando Produtos");
        return Task.CompletedTask;
    }

    private Task EnviarPlotter()
    {
        // TODO: Implementar chamada de API para Plotter usando arquivoProdutos
        Console.WriteLine("Enviando Plotter");
        return Task.CompletedTask;
    }

    private Task EnviarCarePack()
    {
        // TODO: Implementar chamada de API para Care Pack usando arquivoProdutos
        Console.WriteLine("Enviando Care Pack");
        return Task.CompletedTask;
    }

    private Task EnviarPromocao()
    {
        // TODO: Implementar chamada de API para Promoção usando arquivoProdutos
        Console.WriteLine("Enviando Promoção");
        return Task.CompletedTask;
    }


}