using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Text.Json;
using System.Text;
using eCommerce.Shared.Models;

namespace eCommerce.Client.Pages.Cisco;

public partial class CiscoRevisao : ComponentBase
{
    [Parameter]
    public string tipoLista { get; set; } = string.Empty;
    
    [Inject]
    private HttpClient Http { get; set; } = default!;
    
    [Inject]
    private NavigationManager Navigation { get; set; } = default!;
    
    private List<WooProduct> produtos = new();
    private List<WooProduct> produtosFiltrados = new();
    private List<WooProduct> produtosPagina = new();
    private WooProduct? produtoEditando;
    private BancoStats? stats;
    
    // Filtros
    private string buscaProduto = string.Empty;
    private string filtroCategoria = string.Empty;
    private string filtroDisponibilidade = "todos";
    private string tipoEnvio = "todos";
    
    // Paginação
    private int paginaAtual = 1;
    private int produtosPorPagina = 20;
    private int totalPaginas = 1;
    
    // Seleção
    private bool selectAll = false;
    private bool selectAllHeader = false;
    private int produtosSelecionados = 0;
    
    // Modais
    private bool showModalEditar = false;
    private bool showModalConfirmacao = false;
    private string mensagemConfirmacao = string.Empty;
    private Func<Task>? acaoConfirmacao;
    
    // Campos de edição
    private string categoriaEditando = "29";
    private string leadtimeEditando = "local";
    private string urlImagemEditando = string.Empty;
    private string skuLicencaEditando = string.Empty;
    
    // Estado
    private bool isLoading = true;
    
    protected override async Task OnInitializedAsync()
    {
        await CarregarProdutos();
    }
    
    private async Task CarregarProdutos()
    {
        try
        {
            isLoading = true;
            StateHasChanged();
            
            var response = await Http.GetAsync($"/api/cisco/revisao/{tipoLista}/produtos");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<RevisaoResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (data != null)
                {
                    produtos = data.Produtos ?? new List<WooProduct>();
                    produtosFiltrados = new List<WooProduct>(produtos);
                    stats = data.Stats;
                    
                    FiltrarProdutos();
                    ExibirProdutos();
                }
            }
            else
            {
                // Tratar erro
                Console.WriteLine($"Erro ao carregar produtos: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao carregar produtos: {ex.Message}");
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }
    
    private void FiltrarProdutos()
    {
        produtosFiltrados = produtos.Where(produto =>
        {
            // Filtro de busca
            var matchBusca = string.IsNullOrEmpty(buscaProduto) ||
                produto.sku?.ToLower().Contains(buscaProduto.ToLower()) == true ||
                produto.name?.ToLower().Contains(buscaProduto.ToLower()) == true ||
                produto.description?.ToLower().Contains(buscaProduto.ToLower()) == true;
            
            // Filtro de categoria
            var matchCategoria = string.IsNullOrEmpty(filtroCategoria) ||
                (filtroCategoria == "Licenças" && produto.categories?.FirstOrDefault()?.id == 28) ||
                (filtroCategoria == "Produtos Gerais" && produto.categories?.FirstOrDefault()?.id == 29);
            
            // Filtro de disponibilidade (simulado - você pode implementar a lógica real)
            var matchDisponibilidade = filtroDisponibilidade switch
            {
                "disponiveis" => true, // Simulado
                "nao_disponiveis" => false, // Simulado
                "enviados_woocommerce" => true, // Simulado
                "pendentes_woocommerce" => false, // Simulado
                _ => true
            };
            
            return matchBusca && matchCategoria && matchDisponibilidade;
        }).ToList();
        
        paginaAtual = 1;
        ExibirProdutos();
    }
    
    private void ExibirProdutos()
    {
        totalPaginas = (int)Math.Ceiling((double)produtosFiltrados.Count / produtosPorPagina);
        var inicio = (paginaAtual - 1) * produtosPorPagina;
        var fim = Math.Min(inicio + produtosPorPagina, produtosFiltrados.Count);
        
        produtosPagina = produtosFiltrados.Skip(inicio).Take(fim - inicio).ToList();
        StateHasChanged();
    }
    
    private void MudarPagina(int pagina)
    {
        if (pagina >= 1 && pagina <= totalPaginas)
        {
            paginaAtual = pagina;
            ExibirProdutos();
        }
    }
    
    private void MudarProdutosPorPagina()
    {
        paginaAtual = 1;
        ExibirProdutos();
    }
    
    private void ToggleSelectAll()
    {
        foreach (var produto in produtosPagina)
        {
            // Adicionar propriedade Selecionado se não existir
            // Por enquanto, vamos usar uma abordagem diferente
        }
        AtualizarContadorSelecionados();
    }
    
    private void ToggleSelectAllHeader()
    {
        selectAll = selectAllHeader;
        ToggleSelectAll();
    }
    
    private void ToggleProdutoSelecionado(WooProduct produto, object? value)
    {
        if (value is bool isSelected)
        {
            // Adicionar propriedade Selecionado se não existir
            // Por enquanto, vamos usar uma abordagem diferente
            AtualizarContadorSelecionados();
        }
    }
    
    private void AtualizarContadorSelecionados()
    {
        // Implementar contagem de selecionados
        produtosSelecionados = 0; // Simulado
        selectAll = produtosSelecionados == produtosPagina.Count && produtosPagina.Any();
        selectAllHeader = selectAll;
        StateHasChanged();
    }
    
    private string GetImagemUrl(WooProduct produto)
    {
        var imageUrl = produto.meta_data?.FirstOrDefault(m => m.key == "_external_image_url")?.value?.ToString();
        
        if (!string.IsNullOrEmpty(imageUrl) && imageUrl != "nan" && imageUrl.Trim() != "")
        {
            return imageUrl;
        }
        
        return string.Empty;
    }
    
    private void AmpliarImagem(string imageUrl, string? productName)
    {
        // Implementar ampliação de imagem
        Console.WriteLine($"Ampliando imagem: {imageUrl}");
    }
    
    private void EditarProduto(int index)
    {
        if (index >= 0 && index < produtosFiltrados.Count)
        {
            produtoEditando = produtosFiltrados[index];
            
            // Preencher campos de edição
            categoriaEditando = produtoEditando.categories?.FirstOrDefault()?.id.ToString() ?? "29";
            leadtimeEditando = produtoEditando.shipping_class ?? "local";
            
            var imagemMeta = produtoEditando.meta_data?.FirstOrDefault(m => m.key == "_external_image_url");
            urlImagemEditando = imagemMeta?.value?.ToString() ?? string.Empty;
            
            var licencaMeta = produtoEditando.meta_data?.FirstOrDefault(m => m.key == "_license_product");
            skuLicencaEditando = licencaMeta?.value?.ToString() ?? string.Empty;
            
            showModalEditar = true;
            StateHasChanged();
        }
    }
    
    private void BuscarProdutoLicenca()
    {
        var skuBusca = skuLicencaEditando.Trim();
        if (string.IsNullOrEmpty(skuBusca))
        {
            // Mostrar alerta
            return;
        }
        
        var produtoEncontrado = produtosFiltrados.FirstOrDefault(p => 
            p.sku?.ToLower().Contains(skuBusca.ToLower()) == true);
        
        if (produtoEncontrado != null)
        {
            skuLicencaEditando = produtoEncontrado.sku ?? string.Empty;
            StateHasChanged();
        }
    }
    
    private async Task SalvarEdicaoProduto()
    {
        if (produtoEditando != null)
        {
            // Atualizar produto
            var produtoOriginal = produtos.FirstOrDefault(p => p.sku == produtoEditando.sku);
            if (produtoOriginal != null)
            {
                produtoOriginal.name = produtoEditando.name;
                produtoOriginal.sku = produtoEditando.sku;
                produtoOriginal.price = produtoEditando.price;
                produtoOriginal.stock_quantity = produtoEditando.stock_quantity;
                produtoOriginal.description = produtoEditando.description;
                
                // Atualizar categoria
                if (int.TryParse(categoriaEditando, out int categoriaId))
                {
                    produtoOriginal.categories = new List<Category> { new Category { id = categoriaId } };
                }
                
                // Atualizar lead time
                produtoOriginal.shipping_class = leadtimeEditando;
                
                // Atualizar imagem
                if (!string.IsNullOrEmpty(urlImagemEditando))
                {
                    if (produtoOriginal.meta_data == null)
                        produtoOriginal.meta_data = new List<MetaData>();
                    
                    var imagemMeta = produtoOriginal.meta_data.FirstOrDefault(m => m.key == "_external_image_url");
                    if (imagemMeta != null)
                        imagemMeta.value = urlImagemEditando;
                    else
                        produtoOriginal.meta_data.Add(new MetaData { key = "_external_image_url", value = urlImagemEditando });
                }
                
                // Salvar licença associada
                if (!string.IsNullOrEmpty(skuLicencaEditando))
                {
                    if (produtoOriginal.meta_data == null)
                        produtoOriginal.meta_data = new List<MetaData>();
                    
                    var licencaMeta = produtoOriginal.meta_data.FirstOrDefault(m => m.key == "_license_product");
                    if (licencaMeta != null)
                        licencaMeta.value = skuLicencaEditando;
                    else
                        produtoOriginal.meta_data.Add(new MetaData { key = "_license_product", value = skuLicencaEditando });
                }
            }
            
            FecharModalEditar();
            FiltrarProdutos();
        }
    }
    
    private void FecharModalEditar()
    {
        showModalEditar = false;
        produtoEditando = null;
        StateHasChanged();
    }
    
    private void FecharModalConfirmacao()
    {
        showModalConfirmacao = false;
        acaoConfirmacao = null;
        StateHasChanged();
    }
    
    private async Task ConfirmarAcao()
    {
        if (acaoConfirmacao != null)
        {
            await acaoConfirmacao();
        }
        FecharModalConfirmacao();
    }
    
    private async Task SalvarAlteracoes()
    {
        try
        {
            var json = JsonSerializer.Serialize(new { produtos });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await Http.PostAsync($"/api/cisco/revisao/{tipoLista}/salvar", content);
            if (response.IsSuccessStatusCode)
            {
                await CarregarProdutos();
                // Mostrar mensagem de sucesso
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao salvar: {ex.Message}");
        }
    }
    
    private void ConfirmarEnvioWooCommerce()
    {
        var mensagem = tipoEnvio == "selecionados" 
            ? $"Tem certeza que deseja enviar {produtosSelecionados} produtos selecionados para o WooCommerce?"
            : $"Tem certeza que deseja enviar todos os {produtos.Count} produtos para o WooCommerce?";
        
        mensagemConfirmacao = mensagem + " Esta ação não pode ser desfeita.";
        acaoConfirmacao = async () => await EnviarParaWooCommerce();
        showModalConfirmacao = true;
        StateHasChanged();
    }
    
    private async Task EnviarParaWooCommerce()
    {
        try
        {
            var skusParaEnviar = tipoEnvio == "selecionados" 
                ? produtosPagina.Select(p => p.sku).ToList()
                : produtos.Select(p => p.sku).ToList();
            
            var json = JsonSerializer.Serialize(new { skus = skusParaEnviar });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await Http.PostAsync($"/api/cisco/revisao/{tipoLista}/enviar", content);
            if (response.IsSuccessStatusCode)
            {
                // Redirecionar após 3 segundos
                await Task.Delay(3000);
                Navigation.NavigateTo("/cisco");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao enviar: {ex.Message}");
        }
    }
    
    private async Task ExportarBanco()
    {
        try
        {
            var response = await Http.GetAsync($"/api/cisco/banco/{tipoLista}/exportar");
            if (response.IsSuccessStatusCode)
            {
                // Mostrar mensagem de sucesso
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao exportar: {ex.Message}");
        }
    }
    
    private void ConfirmarLimpezaBanco()
    {
        mensagemConfirmacao = $"Tem certeza que deseja limpar completamente o banco {tipoLista.ToUpper()}? Esta ação não pode ser desfeita e removerá TODOS os produtos.";
        acaoConfirmacao = async () => await LimparBanco();
        showModalConfirmacao = true;
        StateHasChanged();
    }
    
    private async Task LimparBanco()
    {
        try
        {
            var response = await Http.PostAsync($"/api/cisco/banco/{tipoLista}/limpar", null);
            if (response.IsSuccessStatusCode)
            {
                await CarregarProdutos();
                // Mostrar mensagem de sucesso
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao limpar banco: {ex.Message}");
        }
    }
    
    private void EditarProdutosSelecionados()
    {
        // Implementar edição em massa
        Console.WriteLine("Edição em massa será implementada em breve");
    }
    
    private void ExportarProdutosSelecionados()
    {
        // Implementar exportação
        Console.WriteLine("Exportação será implementada em breve");
    }
    
    private void VerDetalhes(string sku)
    {
        // Implementar modal de detalhes
        Console.WriteLine($"Detalhes do produto {sku} serão implementados em breve");
    }
    
    // Classes de modelo
    public class RevisaoResponse
    {
        public List<WooProduct>? Produtos { get; set; }
        public BancoStats? Stats { get; set; }
    }
    
    public class BancoStats
    {
        public int TotalProdutos { get; set; }
        public int Disponiveis { get; set; }
        public int NaoDisponiveis { get; set; }
        public int ProdutosComVariacao { get; set; }
    }
}