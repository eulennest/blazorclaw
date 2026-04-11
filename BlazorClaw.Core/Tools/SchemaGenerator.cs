using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;

namespace BlazorClaw.Core.Tools;

public static class SchemaGenerator
{
    public static JsonElement Generate(Type type)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var prop in type.GetProperties())
        {
            var propInfo = new Dictionary<string, object>();

            // Check for Nullable
            Type propType = prop.PropertyType;
            Type underlyingType = Nullable.GetUnderlyingType(propType) ?? propType;

            // Spezialfall: Dictionary
            if (IsDictionaryType(underlyingType))
            {
                propInfo["type"] = "object";
                var dictArgs = underlyingType.GetGenericArguments();
                if (dictArgs.Length == 2)
                {
                    propInfo["additionalProperties"] = new
                    {
                        type = GetTypeName(dictArgs[1])
                    };
                }
            }
            // Spezialfall: Array/List
            else if (underlyingType.IsArray || IsListType(underlyingType))
            {
                propInfo["type"] = "array";
                Type elementType = underlyingType.IsArray
                    ? underlyingType.GetElementType()!
                    : underlyingType.GetGenericArguments().FirstOrDefault() ?? typeof(string);
                propInfo["items"] = new
                {
                    type = GetTypeName(elementType)
                };
            }
            // Spezialfall: Enum
            else if (underlyingType.IsEnum)
            {
                propInfo["type"] = "string";
                propInfo["enum"] = Enum.GetNames(underlyingType);
            }
            // Standard Typ-Mapping
            else
            {
                string typeName = GetTypeName(underlyingType);
                propInfo["type"] = typeName;
            }

            // Beschreibung
            var descAttr = prop.GetCustomAttribute<DescriptionAttribute>();
            if (descAttr != null) propInfo["description"] = descAttr.Description;

            // Range (Min/Max)
            var rangeAttr = prop.GetCustomAttribute<RangeAttribute>();
            if (rangeAttr != null)
            {
                propInfo["minimum"] = rangeAttr.Minimum;
                propInfo["maximum"] = rangeAttr.Maximum;
            }

            // Required Check
            bool isValueType = prop.PropertyType.IsValueType && Nullable.GetUnderlyingType(prop.PropertyType) == null;
            if (prop.GetCustomAttribute<RequiredAttribute>() != null || isValueType)
            {
                required.Add(prop.Name);
            }

            properties[prop.Name] = propInfo;
        }

        var result = new Dictionary<string, object>
        {
            {"type", "object"},
            {"properties", properties}
        };

        if (required.Count > 0)
        {
            result["required"] = required;
        }

        return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(result))!;
    }

    private static bool IsDictionaryType(Type type)
    {
        return type.IsGenericType &&
               (type.GetGenericTypeDefinition() == typeof(Dictionary<,>) ||
                type.GetGenericTypeDefinition() == typeof(IDictionary<,>));
    }

    private static bool IsListType(Type type)
    {
        return type.IsGenericType &&
               (type.GetGenericTypeDefinition() == typeof(List<>) ||
                type.GetGenericTypeDefinition() == typeof(IList<>) ||
                type.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }

    public static string GetTypeName(Type type)
    {
        if (type.IsArray) return "array";
        return type.Name switch
        {
            "String" => "string",
            "Int32" or "Int64" => "integer",
            "Double" or "Decimal" or "Single" => "number",
            "Boolean" => "boolean",
            _ => "object"
        };
    }

}
