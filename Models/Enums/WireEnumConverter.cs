using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FenixLegalOs.Models.Enums;

[AttributeUsage(AttributeTargets.Field)]
public class WireValueAttribute : Attribute
{
    public string Value { get; }
    public WireValueAttribute(string value) => Value = value;
}

public class WireEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    private static readonly Dictionary<string, TEnum> FromWire = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<TEnum, string> ToWire = new();

    static WireEnumConverter()
    {
        foreach (var field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var enumVal = (TEnum)field.GetValue(null)!;
            var attr = field.GetCustomAttribute<WireValueAttribute>();
            var wireStr = attr?.Value ?? enumVal.ToString();
            FromWire[wireStr] = enumVal;
            ToWire[enumVal] = wireStr;
        }
    }

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected string for enum {typeof(TEnum).Name}, got {reader.TokenType}");

        var str = reader.GetString();
        if (str != null && FromWire.TryGetValue(str, out var val))
            return val;

        throw new JsonException($"Unknown wire value '{str}' for enum {typeof(TEnum).Name}. Fail closed.");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        if (ToWire.TryGetValue(value, out var wireStr))
            writer.WriteStringValue(wireStr);
        else
            writer.WriteStringValue(value.ToString());
    }
}
