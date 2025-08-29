using Newtonsoft.Json;
using System.Collections.Generic;

namespace eCommerce.Shared.Models
{
    public class BaseCampoCustomizado
    {
        [JsonProperty("idCampoCustomizado")]
        public long Id { get; set; }

        [JsonProperty("valor")]
        public string Valor { get; set; } = "";

        [JsonProperty("idOpcao")]
        public long? IdOpcao { get; set; }
    }

    public class BaseCampoInfo
    {
        [JsonProperty("id")]
        public long Id { get; set; }
        [JsonProperty("nome")]
        public string Nome { get; set; }
        [JsonProperty("tipoCampo")]
        public TipoCampo TipoCampo { get; set; }
        [JsonProperty("opcoes")]
        public List<BaseOpcaoInfo> Opcoes { get; set; }
    }

    public class TipoCampo
    {
        [JsonProperty("id")]
        public long Id { get; set; }
        [JsonProperty("nome")]
        public string Nome { get; set; }
    }

    public class BaseOpcaoInfo
    {
        [JsonProperty("id")]
        public long Id { get; set; }
        [JsonProperty("nome")]
        public string Nome { get; set; }
    }
} 