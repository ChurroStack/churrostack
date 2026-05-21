using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChurrOS.Api.Utils
{
    public static class JsonSettings
    {
        public static JsonSerializerOptions Value { get; set; } = new JsonSerializerOptions();

        public static void ApplyDefaultOptions(this JsonSerializerOptions options)
        {
            //options.Converters.Add(new JsonStringEnumConverter(new LowerCaseNamingPolicy(), true));
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.AllowOutOfOrderMetadataProperties = true;
        }
    }
}
