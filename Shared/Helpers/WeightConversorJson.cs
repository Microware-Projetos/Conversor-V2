using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace eCommerce.Shared.Helpers;

public class WeightConverter : JsonConverter<string>
{
    public override string ReadJson(JsonReader reader, Type objectType, string existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        // Se o token for string simples, retorna direto
        if (reader.TokenType == JsonToken.String)
        {
            return (string)reader.Value;
        }

        // Se for um objeto, tenta pegar a propriedade "weight"
        if (reader.TokenType == JsonToken.StartObject)
        {
            JObject obj = JObject.Load(reader);
            var weightValue = obj["weight"]?.ToString();
            return weightValue ?? "";
        }

        // Qualquer outro caso retorna string vazia
        return "";
    }

    public override void WriteJson(JsonWriter writer, string value, JsonSerializer serializer)
    {
        // Na hora de serializar, escreve direto como string
        writer.WriteValue(value);
    }
}
