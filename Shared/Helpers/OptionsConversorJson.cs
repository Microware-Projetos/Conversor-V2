using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace eCommerce.Shared.Helpers;

public class FirstOptionConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(string);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.StartArray)
        {
            // Carrega o array e retorna o primeiro valor (ou null se estiver vazio)
            var array = JArray.Load(reader);
            return array.Count > 0 ? array[0].ToString() : null;
        }

        if (reader.TokenType == JsonToken.String)
        {
            return reader.Value?.ToString();
        }

        return null;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        // Escreve como string simples
        writer.WriteValue(value);
    }
}
