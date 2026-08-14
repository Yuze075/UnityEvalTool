#nullable enable
using System;
using System.Globalization;
using UnityEngine;

namespace YuzeToolkit
{
    internal static class DebugToolUtility
    {
        public static void ValidateRequiredToolMetadata(string name, string description)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Debug tool name cannot be empty.", nameof(name));
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Debug tool description cannot be empty.", nameof(description));
            EvalToolRegistry.ValidateToolSegment(name);
        }

        public static void ValidateOptionalToolMetadata(string? name, string? description)
        {
            var hasName = !string.IsNullOrWhiteSpace(name);
            var hasDescription = !string.IsNullOrWhiteSpace(description);
            if (hasName != hasDescription)
                throw new ArgumentException("Debug tool name and description must be provided together.");
            if (hasName) ValidateRequiredToolMetadata(name!, description!);
        }

        public static string ToGeneratedToolName(string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return "Group";
            var chars = label.Trim().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                    continue;
                chars[i] = '_';
            }

            var result = new string(chars).Trim('_');
            return string.IsNullOrWhiteSpace(result) ? "Group" : result;
        }

        public static string GetToolTypeName(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(bool)) return "bool";
            if (type == typeof(int)) return "int";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(string)) return "string";
            if (type == typeof(Vector2)) return "Vector2";
            if (type == typeof(Vector3)) return "Vector3";
            if (type == typeof(Vector4)) return "Vector4";
            if (type == typeof(Color)) return "Color";
            if (type.IsEnum) return type.Name;
            return type.FullName ?? type.Name;
        }

        public static string FormatValue(object? value)
        {
            return value switch
            {
                null => "null",
                float f => f.ToString("0.###", CultureInfo.InvariantCulture),
                double d => d.ToString("0.###", CultureInfo.InvariantCulture),
                Vector2 v => $"({v.x:0.###}, {v.y:0.###})",
                Vector3 v => $"({v.x:0.###}, {v.y:0.###}, {v.z:0.###})",
                Vector4 v => $"({v.x:0.###}, {v.y:0.###}, {v.z:0.###}, {v.w:0.###})",
                Color c => $"RGBA({c.r:0.###}, {c.g:0.###}, {c.b:0.###}, {c.a:0.###})",
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            };
        }

        public static string FormatNumber<TValue>(string format, TValue value)
        {
            if (string.IsNullOrWhiteSpace(format))
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

            try
            {
                if (format.IndexOf("{0", StringComparison.Ordinal) >= 0)
                    return string.Format(CultureInfo.InvariantCulture, format, value);

                return value is IFormattable formattable
                    ? formattable.ToString(format, CultureInfo.InvariantCulture)
                    : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            }
            catch (FormatException)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }
    }
}
