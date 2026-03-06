using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace BlazorClaw.Core.Tools;

public static class SchemaGenerator
{
    public static object Generate(Type type)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var prop in type.GetProperties())
        {
            var propInfo = new Dictionary<string, object>();
            
            // Typ-Mapping
            string typeName = prop.PropertyType.Name switch
            {
                "String" => "string",
                "Int32" or "Int64" => "integer",
                "Double" or "Decimal" or "Single" => "number",
                "Boolean" => "boolean",
                _ => "string"
            };
            propInfo["type"] = typeName;

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
            if (prop.GetCustomAttribute<RequiredAttribute>() != null)
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

        return result;
    }
}
