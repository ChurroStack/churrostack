using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChurrOS.Api.Utils.Converters
{
    public class NumericFlagsEnumConverter<T> : JsonConverter<T> where T : Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetInt32();
            return (T)Enum.ToObject(typeof(T), value);
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            var numericValue = Convert.ToInt32(value);
            writer.WriteNumberValue(numericValue);
        }
    }
}
