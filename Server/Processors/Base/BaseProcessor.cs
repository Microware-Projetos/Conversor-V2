using eCommerce.Shared.Models;
using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LiteDB;
using WooAttribute = eCommerce.Shared.Models.Attribute;
using System.Threading;
using System.Net.Http;

namespace eCommerce.Server.Processors.Base;

public static class BaseProcessor
{
    private static List<WordPressCategory>? _wordPressCategories;
    private static List<BaseCategory>? _BaseCategories;
    private static List<AttributeMap>? _attributeMaps;

    // Dicionários globais para mapeamento
    private static Dictionary<string, long> campoNomeParaId = CarregarCamposCustomizados();

    private static HttpClient _httpClient = new HttpClient();

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

            var produtosBase = new List<BaseProduct>();

            foreach (var produto in produtos)
            {
                // Verificar cancelamento a cada produto
                cancellationToken.ThrowIfCancellationRequested();
                
                // Validar se o produto tem nome
                if (string.IsNullOrWhiteSpace(produto.name))
                {
                    continue; // Pular produtos sem nome
                }

                var produtoBase = new BaseProduct
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
                    Categoria = new BaseCategoria
                    {
                        Id = ObterIdCategoriaBase(produto.categories)
                    },
                    Estoque = new BaseEstoque
                    {
                        Minimo = 1,
                        Maximo = 10,
                        CrossDocking = ShippingTime(produto.shipping_class)
                    },
                    Dimensoes = new BaseDimensoes
                    {
                        Largura = decimal.TryParse(produto.dimensions.width, out var largura) ? largura : 0,
                        Altura = decimal.TryParse(produto.dimensions.height, out var altura) ? altura : 0,
                        Profundidade = decimal.TryParse(produto.dimensions.length, out var profundidade) ? profundidade : 0,
                        UnidadeMedida = 2
                    },
                    Midia = new BaseMidia
                    {
                        Imagens = new BaseImagens
                        {
                            ImagensURL = ExtrairImagensDoProduto(produto.meta_data)
                        }
                    },
                    CamposCustomizados = MontarCamposCustomizados(produto)
                };
                
                // Verificar se o produto tem imagens antes de adicionar à lista
                if (produtoBase.Midia?.Imagens?.ImagensURL == null || !produtoBase.Midia.Imagens.ImagensURL.Any())
                {
                    Console.WriteLine($"⚠️ Produto {produtoBase.Codigo} não tem imagens. Verificando meta_data...");
                    
                    // Tentar extrair imagens novamente com mais detalhes
                    var imagens = ExtrairImagensDoProduto(produto.meta_data);
                    if (imagens.Any())
                    {
                        produtoBase.Midia.Imagens.ImagensURL = imagens;
                        Console.WriteLine($"✅ Imagens encontradas na segunda tentativa para {produtoBase.Codigo}");
                    }
                    else
                    {
                        Console.WriteLine($"❌ Produto {produtoBase.Codigo} será pulado por não ter imagens");
                        continue; // Pular produtos sem imagens
                    }
                }
                
                Console.WriteLine($"✅ Produto {produtoBase.Codigo} processado com {produtoBase.Midia.Imagens.ImagensURL.Count} imagens");
                produtosBase.Add(produtoBase);
            }
            
            //Enviar produtos para o Base
            await EnviarProdutosParaBase(produtosBase, cancellationToken);
        }
    }

    private static async Task EnviarProdutosParaBase(List<BaseProduct> produtosBase, CancellationToken cancellationToken)
    {
        // Obter token do banco de dados
        var token = ObterTokenDoBanco();
        if (token == null || string.IsNullOrEmpty(token.AccessToken))
        {
            throw new InvalidOperationException("Token de acesso não encontrado. Execute a autorização OAuth primeiro.");
        }

        var url = "https://api.Base.com.br/Api/v3/produtos";
        using var client = new HttpClient();
        
        // Configurar autenticação com Bearer token
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);
        
        // Configurar timeout maior para evitar timeouts
        client.Timeout = TimeSpan.FromMinutes(2);

        // Processar produtos em lotes de 2 (reduzido para evitar rate limit)
        var batchSize = 2;
        var semaphore = new SemaphoreSlim(batchSize, batchSize);
        var tasks = new List<Task>();

        foreach (var produto in produtosBase)
        {
            var task = ProcessarProdutoComRateLimit(client, url, produto, semaphore, cancellationToken);
            tasks.Add(task);
        }

        // Aguardar todos os produtos serem processados
        await Task.WhenAll(tasks);
    }

    private static async Task ProcessarProdutoComRateLimit(HttpClient client, string url, BaseProduct produto, SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        
        try
        {
            // Aguardar 1 segundo para respeitar o limite de 2 requisições por segundo (mais conservador)
            await Task.Delay(1000, cancellationToken);
            
            await EnviarProdutoComRetry(client, url, produto, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static async Task EnviarProdutoComRetry(HttpClient client, string url, BaseProduct produto, CancellationToken cancellationToken, int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var json = JsonConvert.SerializeObject(produto);
                Console.WriteLine($"📤 Enviando produto {produto.Codigo}:");
                Console.WriteLine($"   - Nome: {produto.Nome}");
                Console.WriteLine($"   - Preço: {produto.Preco}");
                Console.WriteLine($"   - Imagens: {produto.Midia?.Imagens?.ImagensURL?.Count ?? 0}");
                if (produto.Midia?.Imagens?.ImagensURL?.Any() == true)
                {
                    foreach (var img in produto.Midia.Imagens.ImagensURL)
                    {
                        Console.WriteLine($"     - {img.Link}");
                    }
                }
                
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✅ Produto {produto.Codigo} enviado com sucesso");
                    Console.WriteLine($"📥 Resposta da criação: {responseBody}");
                    
                    // Deserializar resposta para obter o ID do produto
                    var produtoResponse = JsonConvert.DeserializeObject<BaseProdutoResponse>(responseBody);
                    if (produtoResponse?.data?.id != null)
                    {
                        var produtoId = produtoResponse.data.id;
                        Console.WriteLine($"ID do produto {produto.Codigo}: {produtoId}");
                        
                        // Aguardar um pouco antes de adicionar estoque para evitar conflitos
                        await Task.Delay(3000, cancellationToken);
                        
                        // Verificar se o produto foi criado corretamente com fotos
                        var temImagens = await VerificarProdutoCriado(client, produtoId, produto.Codigo, cancellationToken);
                        
                        // Se o produto não tem imagens, tentar atualizar
                        if (!temImagens)
                        {
                            await TentarAtualizarImagensProduto(client, produtoId, produto, cancellationToken);
                            
                            // Aguardar um pouco para a API processar a atualização
                            await Task.Delay(2000, cancellationToken);
                            
                            // Verificar novamente se as imagens foram atualizadas
                            var imagensAtualizadas = await VerificarProdutoCriado(client, produtoId, produto.Codigo, cancellationToken);
                            if (imagensAtualizadas)
                            {
                                Console.WriteLine($"✅ Imagens do produto {produto.Codigo} foram atualizadas com sucesso!");
                            }
                            else
                            {
                                Console.WriteLine($"❌ Falha ao atualizar imagens do produto {produto.Codigo}");
                            }
                        }
                        
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
        Console.WriteLine($"Iniciando adição de estoque para produto ID {produtoId} com quantidade {quantidade}");
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Aguardar um pouco antes de tentar adicionar estoque para evitar conflitos
                await Task.Delay(2000, cancellationToken);
                
                var url = "https://api.Base.com.br/Api/v3/estoques";
                
                var estoqueRequest = new
                {
                    deposito = new { id = 14888365761 },
                    produto = new { id = produtoId },
                    quantidade = quantidade,
                    operacao = "B"
                };
                
                var json = JsonConvert.SerializeObject(estoqueRequest);
                Console.WriteLine($"Enviando requisição de estoque (tentativa {attempt}): {json}");
                
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                
                Console.WriteLine($"Resposta do estoque (tentativa {attempt}): {response.StatusCode} - {responseBody}");
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"✅ Estoque adicionado com sucesso para o produto ID {produtoId}");
                    return; // Sucesso, sair do loop de retry
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    var waitTime = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    Console.WriteLine($"⚠️ Rate limit atingido para estoque do produto {produtoId}. Aguardando {waitTime.TotalSeconds}s");
                    await Task.Delay(waitTime, cancellationToken);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.GatewayTimeout || 
                         response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                {
                    var waitTime = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    Console.WriteLine($"⚠️ Timeout para estoque do produto {produtoId}. Aguardando {waitTime.TotalSeconds}s");
                    await Task.Delay(waitTime, cancellationToken);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    Console.WriteLine($"⚠️ Conflito ao adicionar estoque para produto {produtoId}. Produto pode já ter estoque.");
                    return; // Não tentar novamente em caso de conflito
                }
                else
                {
                    Console.WriteLine($"❌ Erro ao adicionar estoque para produto ID {produtoId}: {response.StatusCode} - {responseBody}");
                    if (attempt == maxRetries)
                    {
                        Console.WriteLine($"❌ Falha definitiva ao adicionar estoque para produto {produtoId} após {maxRetries} tentativas");
                    }
                    else
                    {
                        var waitTime = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                        Console.WriteLine($"Aguardando {waitTime.TotalSeconds}s antes da próxima tentativa");
                        await Task.Delay(waitTime, cancellationToken);
                    }
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                if (attempt < maxRetries)
                {
                    var waitTime = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    Console.WriteLine($"⚠️ Timeout da requisição para estoque do produto {produtoId}. Aguardando {waitTime.TotalSeconds}s");
                    await Task.Delay(waitTime, cancellationToken);
                }
                else
                {
                    Console.WriteLine($"❌ Erro de timeout após {maxRetries} tentativas para estoque do produto {produtoId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao adicionar estoque para produto ID {produtoId} (tentativa {attempt}): {ex.Message}");
                if (attempt == maxRetries)
                {
                    Console.WriteLine($"❌ Falha definitiva ao adicionar estoque para produto {produtoId} após {maxRetries} tentativas");
                }
                else
                {
                    var waitTime = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    Console.WriteLine($"Aguardando {waitTime.TotalSeconds}s antes da próxima tentativa");
                    await Task.Delay(waitTime, cancellationToken);
                }
            }
        }
    }

    private static async Task<bool> VerificarProdutoCriado(HttpClient client, long produtoId, string codigoProduto, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://api.Base.com.br/Api/v3/produtos/{produtoId}";
            Console.WriteLine($"🔍 Verificando produto {codigoProduto} (ID: {produtoId})...");
            
            var response = await client.GetAsync(url, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Produto {codigoProduto} (ID: {produtoId}) encontrado na API");
                
                // Tentar deserializar a resposta completa para extrair informações das imagens
                try
                {
                    var produtoCompleto = JsonConvert.DeserializeObject<BaseProdutoCompletoResponse>(responseBody);
                    
                    if (produtoCompleto?.data != null)
                    {
                        var produto = produtoCompleto.data;
                        Console.WriteLine($"📋 Informações do produto {codigoProduto}:");
                        Console.WriteLine($"   - Nome: {produto.nome}");
                        Console.WriteLine($"   - Código: {produto.codigo}");
                        Console.WriteLine($"   - Preço: {produto.preco}");
                        
                        // Verificar imagens
                        if (produto.imagens != null && produto.imagens.Any())
                        {
                            Console.WriteLine($"🖼️ Produto {codigoProduto} tem {produto.imagens.Count} imagens:");
                            foreach (var imagem in produto.imagens)
                            {
                                Console.WriteLine($"   - {imagem.link}");
                            }
                            return true; // Produto tem imagens
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ Produto {codigoProduto} foi criado mas NÃO tem imagens!");
                            return false; // Produto não tem imagens
                        }
                        
                        // Verificar estoque
                        if (produto.estoque != null)
                        {
                            Console.WriteLine($"📦 Estoque do produto {codigoProduto}: {produto.estoque.quantidade}");
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ Produto {codigoProduto} não tem informações de estoque");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"❌ Não foi possível extrair dados do produto {codigoProduto} da resposta");
                        Console.WriteLine($"Resposta recebida: {responseBody}");
                        return false; // Produto não tem imagens
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Erro ao processar resposta do produto {codigoProduto}: {ex.Message}");
                    Console.WriteLine($"Resposta recebida: {responseBody}");
                    return false; // Produto não tem imagens
                }
            }
            else
            {
                Console.WriteLine($"❌ Falha ao verificar produto {codigoProduto} (ID: {produtoId}): {response.StatusCode}");
                Console.WriteLine($"Resposta de erro: {responseBody}");
                return false; // Produto não tem imagens
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao verificar produto {codigoProduto} (ID: {produtoId}): {ex.Message}");
            return false; // Produto não tem imagens
        }
    }

    private static async Task TentarAtualizarImagensProduto(HttpClient client, long produtoId, BaseProduct produto, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://api.Base.com.br/Api/v3/produtos/{produtoId}";
            Console.WriteLine($"🔄 Tentando atualizar imagens do produto {produto.Codigo} (ID: {produtoId})...");

            // Verificar se o produto tem imagens para enviar
            if (produto.Midia?.Imagens?.ImagensURL == null || !produto.Midia.Imagens.ImagensURL.Any())
            {
                Console.WriteLine($"❌ Produto {produto.Codigo} não tem imagens para atualizar!");
                return;
            }

            // Criar objeto com a estrutura correta da API do Base (PATCH)
            var patchBody = new
            {
                imagens = new
                {
                    imagensURL = produto.Midia.Imagens.ImagensURL.Select(img => new { link = img.Link }).ToList()
                }
            };

            var json = JsonConvert.SerializeObject(patchBody);
            Console.WriteLine($"📤 Enviando PATCH de imagens: {json}");
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
            var response = await client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            Console.WriteLine($"📥 Resposta do PATCH: {response.StatusCode} - {responseBody}");

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"✅ Imagens do produto {produto.Codigo} (ID: {produtoId}) atualizadas com sucesso.");
            }
            else
            {
                Console.WriteLine($"❌ Falha ao atualizar imagens do produto {produto.Codigo} (ID: {produtoId}): {response.StatusCode} - {responseBody}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao tentar atualizar imagens do produto {produto.Codigo} (ID: {produtoId}): {ex.Message}");
        }
    }

    private static BaseToken? ObterTokenDoBanco()
    {
        try
        {
            using var db = new LiteDatabase("Filename=fila.db;Connection=shared");
            var tokenCollection = db.GetCollection<BaseToken>("Base_tokens");
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

        // Carregar categorias do Base
        var BaseJson = File.ReadAllText("Maps/Base/categories.json");
        _BaseCategories = JsonConvert.DeserializeObject<List<BaseCategory>>(BaseJson);
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

    private static long ObterIdCategoriaBase(List<Category>? categoriasProduto)
    {
        if (categoriasProduto == null || !categoriasProduto.Any() || _wordPressCategories == null || _BaseCategories == null)
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

        // Buscar o ID correspondente no mapeamento do Base pelo nome
        var categoriaBase = _BaseCategories.FirstOrDefault(c => 
            string.Equals(c.descricao, categoriaWordPress.name, StringComparison.OrdinalIgnoreCase));
        
        return categoriaBase?.id ?? 0;
    }

    private static List<BaseImagemURL> ExtrairImagensDoProduto(List<MetaData> meta_data)
    {
        var imagensURL = new List<BaseImagemURL>();

        Console.WriteLine($"🔍 Iniciando extração de imagens. Meta_data count: {meta_data?.Count ?? 0}");

        if (meta_data == null || !meta_data.Any())
        {
            Console.WriteLine("❌ Meta_data está vazio ou nulo");
            return imagensURL;
        }

        // Log de todas as chaves disponíveis para debug
        Console.WriteLine("📋 Chaves disponíveis no meta_data:");
        foreach (var meta in meta_data)
        {
            Console.WriteLine($"   - {meta.key}: {meta.value}");
        }

        // Buscar imagem principal (_external_image_url)
        var imagemPrincipal = meta_data.FirstOrDefault(m => m.key == "_external_image_url");
        if (imagemPrincipal?.value != null)
        {
            var urlImagem = imagemPrincipal.value.ToString();
            Console.WriteLine($"🔍 Imagem principal encontrada: {urlImagem}");
            
            if (!string.IsNullOrWhiteSpace(urlImagem) && IsValidImageUrl(urlImagem))
            {
                imagensURL.Add(new BaseImagemURL { Link = urlImagem.Trim() });
                Console.WriteLine($"✅ Imagem principal adicionada: {urlImagem}");
            }
            else
            {
                Console.WriteLine($"❌ URL da imagem principal inválida: {urlImagem}");
            }
        }
        else
        {
            Console.WriteLine("⚠️ Imagem principal (_external_image_url) não encontrada");
        }

        // Buscar galeria de imagens (_external_gallery_images)
        var galeriaImagens = meta_data.FirstOrDefault(m => m.key == "_external_gallery_images");
        if (galeriaImagens?.value != null)
        {
            Console.WriteLine($"🔍 Galeria de imagens encontrada: {galeriaImagens.value}");
            
            try
            {
                // Tentar deserializar como array de strings
                var galeriaJson = galeriaImagens.value.ToString();
                Console.WriteLine($"🔄 Tentando processar galeria: {galeriaJson}");
                
                var imagensGaleria = JsonConvert.DeserializeObject<List<string>>(galeriaJson);
                
                if (imagensGaleria != null && imagensGaleria.Any())
                {
                    Console.WriteLine($"📸 Galeria deserializada com {imagensGaleria.Count} imagens");
                    foreach (var imagem in imagensGaleria)
                    {
                        if (!string.IsNullOrWhiteSpace(imagem) && IsValidImageUrl(imagem))
                        {
                            imagensURL.Add(new BaseImagemURL { Link = imagem.Trim() });
                            Console.WriteLine($"✅ Imagem da galeria adicionada: {imagem}");
                        }
                        else
                        {
                            Console.WriteLine($"❌ URL da imagem da galeria inválida: {imagem}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("⚠️ Galeria deserializada está vazia, tentando como string única");
                    // Se falhar na deserialização como lista, tentar como string única
                    var imagemUnica = galeriaImagens.value.ToString();
                    if (!string.IsNullOrWhiteSpace(imagemUnica) && IsValidImageUrl(imagemUnica))
                    {
                        imagensURL.Add(new BaseImagemURL { Link = imagemUnica.Trim() });
                        Console.WriteLine($"✅ Imagem única da galeria adicionada: {imagemUnica}");
                    }
                    else
                    {
                        Console.WriteLine($"❌ URL da imagem única inválida: {imagemUnica}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao processar galeria de imagens: {ex.Message}");
                // Se falhar na deserialização, tentar como string única
                var imagemUnica = galeriaImagens.value.ToString();
                if (!string.IsNullOrWhiteSpace(imagemUnica) && IsValidImageUrl(imagemUnica))
                {
                    imagensURL.Add(new BaseImagemURL { Link = imagemUnica.Trim() });
                    Console.WriteLine($"✅ Imagem única da galeria (fallback) adicionada: {imagemUnica}");
                }
                else
                {
                    Console.WriteLine($"❌ URL da imagem única (fallback) inválida: {imagemUnica}");
                }
            }
        }
        else
        {
            Console.WriteLine("⚠️ Galeria de imagens (_external_gallery_images) não encontrada");
        }

        // Verificar se encontrou alguma imagem
        if (!imagensURL.Any())
        {
            Console.WriteLine("❌ ERRO: Nenhuma imagem foi extraída do produto!");
        }
        else
        {
            Console.WriteLine($"✅ Total de imagens extraídas: {imagensURL.Count}");
        }

        return imagensURL;
    }

    private static bool IsValidImageUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
            
        // Verificar se é uma URL válida
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
            
        // Verificar se é HTTP ou HTTPS
        if (uri.Scheme != "http" && uri.Scheme != "https")
            return false;
            
        // Verificar se tem extensão de imagem comum
        var extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
        var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };
        
        return validExtensions.Contains(extension);
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

    private static Dictionary<string, long> CarregarCamposCustomizados()
    {
        var camposJson = File.ReadAllText("Maps/Base/campos.json");
        var camposBase = JsonConvert.DeserializeObject<List<BaseCampoInfo>>(camposJson);
        return camposBase.ToDictionary(c => c.Nome, c => c.Id);
    }

    private static List<BaseCampoCustomizado> MontarCamposCustomizados(WooProduct produto)
    {
        var camposCustomizados = new List<BaseCampoCustomizado>();
        foreach (var attribute in produto.attributes)
        {
            var attributeMap = _attributeMaps.FirstOrDefault(a => a.id == attribute.id);
            if (attributeMap == null || string.IsNullOrWhiteSpace(attribute.options))
                continue;
            var nomeCampo = attributeMap.name;
            if (campoNomeParaId.TryGetValue(nomeCampo, out var idCampo))
            {
                camposCustomizados.Add(new BaseCampoCustomizado
                {
                    Id = idCampo,
                    Valor = attribute.options
                });
            }
        }
        return camposCustomizados;
    }
}