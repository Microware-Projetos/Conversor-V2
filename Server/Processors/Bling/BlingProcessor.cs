using eCommerce.Shared.Models;
using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LiteDB;
using WooAttribute = eCommerce.Shared.Models.Attribute;
using System.Threading;

namespace eCommerce.Server.Processors.Bling;

public static class BlingProcessor
{
    private static List<WordPressCategory>? _wordPressCategories;
    private static List<BlingCategory>? _blingCategories;
    private static List<AttributeMap>? _attributeMaps;

    public static async Task ProcessarProdutos(string loja, CancellationToken cancellationToken = default)
    {
       if (loja == "HP")
       {
            // Verificar cancelamento
            cancellationToken.ThrowIfCancellationRequested();
            
            // Carregar mapeamentos de categorias e atributos
            CarregarMapeamentosCategorias();
            CarregarMapeamentosAtributos();

            //Ler arquivo produtosHP.json
            var json = File.ReadAllText("produtosHP.json");
            var produtos = JsonConvert.DeserializeObject<List<WooProduct>>(json);

            var produtosBling = new List<BlingProduct>();

            foreach (var produto in produtos)
            {
                // Verificar cancelamento a cada produto
                cancellationToken.ThrowIfCancellationRequested();
                
                // Validar se o produto tem nome
                if (string.IsNullOrWhiteSpace(produto.name))
                {
                    continue; // Pular produtos sem nome
                }

                var produtoBling = new BlingProduct
                {
                    Nome = produto.name.Trim(),
                    Codigo = !string.IsNullOrWhiteSpace(produto.sku) ? produto.sku.Trim() : produto.name.Trim(),
                    Preco = decimal.TryParse(produto.regular_price, out var preco) ? preco : 0,
                    DescricaoCurta = !string.IsNullOrWhiteSpace(produto.short_description) ? produto.short_description.Trim() : "",
                    PesoLiquido = decimal.TryParse(produto.weight, out var pesoLiquido) ? pesoLiquido : 0,
                    PesoBruto = decimal.TryParse(produto.weight, out var pesoBruto) ? pesoBruto : 0,
                    Marca = "HP",
                    Gtin = ObterEanDosAtributos(produto.attributes),
                    DescricaoComplementar = !string.IsNullOrWhiteSpace(produto.description) ? produto.description.Trim() : "",
                    Observacoes = ProcessarObservacoesAtributos(produto.attributes),
                    Categoria = new BlingCategoria
                    {
                        Id = ObterIdCategoriaBling(produto.categories)
                    },
                    Estoque = new BlingEstoque
                    {
                        Minimo = 1,
                        Maximo = 10,
                        CrossDocking = ShippingTime(produto.shipping_class)
                    },
                    Dimensoes = new BlingDimensoes
                    {
                        Largura = decimal.TryParse(produto.dimensions.width, out var largura) ? largura : 0,
                        Altura = decimal.TryParse(produto.dimensions.height, out var altura) ? altura : 0,
                        Profundidade = decimal.TryParse(produto.dimensions.length, out var profundidade) ? profundidade : 0,
                        UnidadeMedida = 2
                    },
                    Midia = new BlingMidia
                    {
                        Imagens = new BlingImagens
                        {
                            ImagensURL = ExtrairImagensDoProduto(produto.meta_data)
                        }
                    }
                };
                
                produtosBling.Add(produtoBling);
            }
            
            //Enviar produtos para o Bling
            await EnviarProdutosParaBling(produtosBling, cancellationToken);
        }
    }

    private static async Task EnviarProdutosParaBling(List<BlingProduct> produtosBling, CancellationToken cancellationToken)
    {
        // Obter token do banco de dados
        var token = ObterTokenDoBanco();
        if (token == null || string.IsNullOrEmpty(token.AccessToken))
        {
            throw new InvalidOperationException("Token de acesso não encontrado. Execute a autorização OAuth primeiro.");
        }

        var url = "https://api.bling.com.br/Api/v3/produtos";
        using var client = new HttpClient();
        
        // Configurar autenticação com Bearer token
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);
        
        // Configurar timeout maior para evitar timeouts
        client.Timeout = TimeSpan.FromMinutes(2);

        // Processar produtos em lotes de 3 (limite da API)
        var batchSize = 3;
        var semaphore = new SemaphoreSlim(batchSize, batchSize);
        var tasks = new List<Task>();

        foreach (var produto in produtosBling)
        {
            var task = ProcessarProdutoComRateLimit(client, url, produto, semaphore, cancellationToken);
            tasks.Add(task);
        }

        // Aguardar todos os produtos serem processados
        await Task.WhenAll(tasks);
    }

    private static async Task ProcessarProdutoComRateLimit(HttpClient client, string url, BlingProduct produto, SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        
        try
        {
            // Aguardar 1/3 de segundo para respeitar o limite de 3 requisições por segundo
            await Task.Delay(333, cancellationToken);
            
            await EnviarProdutoComRetry(client, url, produto, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static async Task EnviarProdutoComRetry(HttpClient client, string url, BlingProduct produto, CancellationToken cancellationToken, int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var json = JsonConvert.SerializeObject(produto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Produto {produto.Codigo} enviado com sucesso");
                    
                    // Deserializar resposta para obter o ID do produto
                    var produtoResponse = JsonConvert.DeserializeObject<BlingProdutoResponse>(responseBody);
                    if (produtoResponse?.data?.id != null)
                    {
                        var produtoId = produtoResponse.data.id;
                        Console.WriteLine($"ID do produto {produto.Codigo}: {produtoId}");
                        
                        // Adicionar estoque ao produto com retry
                        await AdicionarEstoqueComRetry(client, produtoId, produto.Estoque.Maximo, cancellationToken);
                    }
                    return; // Sucesso, sair do loop de retry
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    // Rate limit atingido, aguardar mais tempo
                    var waitTime = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // Exponential backoff
                    Console.WriteLine($"Rate limit atingido para {produto.Codigo}. Aguardando {waitTime.TotalSeconds}s antes da tentativa {attempt + 1}");
                    await Task.Delay(waitTime, cancellationToken);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.GatewayTimeout || 
                         response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                {
                    // Timeout, aguardar antes de tentar novamente
                    var waitTime = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    Console.WriteLine($"Timeout para {produto.Codigo}. Aguardando {waitTime.TotalSeconds}s antes da tentativa {attempt + 1}");
                    await Task.Delay(waitTime, cancellationToken);
                }
                else
                {
                    Console.WriteLine($"Erro ao enviar produto {produto.Codigo}: {response.StatusCode} - {responseBody}");
                    return; // Erro não recuperável, não tentar novamente
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                // Timeout da requisição
                if (attempt < maxRetries)
                {
                    var waitTime = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    Console.WriteLine($"Timeout da requisição para {produto.Codigo}. Aguardando {waitTime.TotalSeconds}s antes da tentativa {attempt + 1}");
                    await Task.Delay(waitTime, cancellationToken);
                }
                else
                {
                    Console.WriteLine($"Erro de timeout após {maxRetries} tentativas para {produto.Codigo}: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao enviar produto {produto.Codigo} (tentativa {attempt}): {ex.Message}");
                if (attempt == maxRetries)
                {
                    Console.WriteLine($"Falha definitiva ao enviar produto {produto.Codigo} após {maxRetries} tentativas");
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
                }
            }
        }
    }

    private static async Task AdicionarEstoqueComRetry(HttpClient client, long produtoId, int quantidade, CancellationToken cancellationToken, int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var url = "https://api.bling.com.br/Api/v3/estoques";
                
                var estoqueRequest = new
                {
                    deposito = new { id = 14888365761 },
                    produto = new { id = produtoId },
                    quantidade = quantidade,
                    operacao = "B"
                };
                
                var json = JsonConvert.SerializeObject(estoqueRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Estoque adicionado com sucesso para o produto ID {produtoId}");
                    return; // Sucesso, sair do loop de retry
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    var waitTime = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    Console.WriteLine($"Rate limit atingido para estoque do produto {produtoId}. Aguardando {waitTime.TotalSeconds}s");
                    await Task.Delay(waitTime, cancellationToken);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.GatewayTimeout || 
                         response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                {
                    var waitTime = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    Console.WriteLine($"Timeout para estoque do produto {produtoId}. Aguardando {waitTime.TotalSeconds}s");
                    await Task.Delay(waitTime, cancellationToken);
                }
                else
                {
                    Console.WriteLine($"Erro ao adicionar estoque para produto ID {produtoId}: {response.StatusCode} - {responseBody}");
                    return; // Erro não recuperável
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                if (attempt < maxRetries)
                {
                    var waitTime = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    Console.WriteLine($"Timeout da requisição para estoque do produto {produtoId}. Aguardando {waitTime.TotalSeconds}s");
                    await Task.Delay(waitTime, cancellationToken);
                }
                else
                {
                    Console.WriteLine($"Erro de timeout após {maxRetries} tentativas para estoque do produto {produtoId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao adicionar estoque para produto ID {produtoId} (tentativa {attempt}): {ex.Message}");
                if (attempt == maxRetries)
                {
                    Console.WriteLine($"Falha definitiva ao adicionar estoque para produto {produtoId} após {maxRetries} tentativas");
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
                }
            }
        }
    }

    private static BlingToken? ObterTokenDoBanco()
    {
        try
        {
            using var db = new LiteDatabase("Filename=fila.db;Connection=shared");
            var tokenCollection = db.GetCollection<BlingToken>("bling_tokens");
            var token = tokenCollection.Query().FirstOrDefault();
            
            if (token != null && !token.IsExpired)
            {
                return token;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao obter token do banco: {ex.Message}");
            return null;
        }
    }

    private static int ShippingTime(string? tempo){
        if (tempo == "importado")
        {
            return 48;
        }else{
            return 36;
        }
    }

    private static void CarregarMapeamentosCategorias()
    {
        // Carregar categorias do WordPress
        var wordPressJson = File.ReadAllText("Maps/HP/categoriesWordpress.json");
        _wordPressCategories = JsonConvert.DeserializeObject<List<WordPressCategory>>(wordPressJson);

        // Carregar categorias do Bling
        var blingJson = File.ReadAllText("Maps/Bling/categories.json");
        _blingCategories = JsonConvert.DeserializeObject<List<BlingCategory>>(blingJson);
    }

    private static void CarregarMapeamentosAtributos()
    {
        try
        {
            // Carregar mapeamento de atributos
            var attributesJson = File.ReadAllText("Maps/HP/atributes.json");
            _attributeMaps = JsonConvert.DeserializeObject<List<AttributeMap>>(attributesJson);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao carregar mapeamento de atributos: {ex.Message}");
            _attributeMaps = new List<AttributeMap>();
        }
    }

    private static long ObterIdCategoriaBling(List<Category>? categoriasProduto)
    {
        if (categoriasProduto == null || !categoriasProduto.Any() || _wordPressCategories == null || _blingCategories == null)
        {
            return 0; // Categoria padrão ou sem categoria
        }

        // Pegar a primeira categoria do produto
        var categoriaProduto = categoriasProduto.First();
        
        // Buscar o nome da categoria no mapeamento do WordPress
        var categoriaWordPress = _wordPressCategories.FirstOrDefault(c => c.id == categoriaProduto.id);
        
        if (categoriaWordPress == null)
        {
            return 0; // Categoria não encontrada no mapeamento
        }

        // Buscar o ID correspondente no mapeamento do Bling pelo nome
        var categoriaBling = _blingCategories.FirstOrDefault(c => 
            string.Equals(c.descricao, categoriaWordPress.name, StringComparison.OrdinalIgnoreCase));
        
        return categoriaBling?.id ?? 0;
    }

    private static List<BlingImagemURL> ExtrairImagensDoProduto(List<MetaData> meta_data)
    {
        var imagensURL = new List<BlingImagemURL>();

        // Buscar imagem principal (_external_image_url)
        var imagemPrincipal = meta_data.FirstOrDefault(m => m.key == "_external_image_url");
        if (imagemPrincipal?.value != null)
        {
            imagensURL.Add(new BlingImagemURL { Link = imagemPrincipal.value.ToString() });
        }

        // Buscar galeria de imagens (_external_gallery_images)
        var galeriaImagens = meta_data.FirstOrDefault(m => m.key == "_external_gallery_images");
        if (galeriaImagens?.value != null)
        {
            try
            {
                // Tentar deserializar como array de strings
                var galeriaJson = galeriaImagens.value.ToString();
                var imagensGaleria = JsonConvert.DeserializeObject<List<string>>(galeriaJson);
                
                if (imagensGaleria != null)
                {
                    foreach (var imagem in imagensGaleria)
                    {
                        imagensURL.Add(new BlingImagemURL { Link = imagem });
                    }
                }
            }
            catch (Exception ex)
            {
                // Se falhar na deserialização, tentar como string única
                var imagemUnica = galeriaImagens.value.ToString();
                if (!string.IsNullOrEmpty(imagemUnica))
                {
                    imagensURL.Add(new BlingImagemURL { Link = imagemUnica });
                }
            }
        }

        return imagensURL;
    }

    private static string ProcessarObservacoesAtributos(List<WooAttribute> attributes)
    {
        if (attributes == null || !attributes.Any() || _attributeMaps == null)
        {
            return "";
        }

        var observacoes = new List<string>();

        foreach (var attribute in attributes)
        {
            // Buscar o nome do atributo pelo ID no mapeamento
            var attributeMap = _attributeMaps.FirstOrDefault(a => a.id == attribute.id);
            if (attributeMap != null && !string.IsNullOrWhiteSpace(attribute.options))
            {
                var observacao = $"{attributeMap.name}: {attribute.options}";
                observacoes.Add(observacao);
            }
        }

        // Juntar todas as observações com quebra de linha
        return string.Join("\n", observacoes);
    }

    private static string ObterEanDosAtributos(List<WooAttribute> attributes)
    {
        if (attributes == null || !attributes.Any())
        {
            return "";
        }

        // Buscar o atributo com ID 13 (Código EAN)
        var eanAttribute = attributes.FirstOrDefault(a => a.id == 13);
        return !string.IsNullOrWhiteSpace(eanAttribute?.options) ? eanAttribute.options.Trim() : "";
    }
}

// Classes auxiliares para deserialização dos JSONs
public class WordPressCategory
{
    public int id { get; set; }
    public string name { get; set; } = "";
}

public class BlingCategory
{
    public long id { get; set; }
    public string descricao { get; set; } = "";
    public CategoriaPai categoriaPai { get; set; } = new();
}

public class CategoriaPai
{
    public long id { get; set; }
}

// Classes para deserializar resposta da API do Bling
public class BlingProdutoResponse
{
    public BlingProdutoData? data { get; set; }
}

public class BlingProdutoData
{
    public long id { get; set; }
    public object? variations { get; set; }
    public List<string>? warnings { get; set; }
}