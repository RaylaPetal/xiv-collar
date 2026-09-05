using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Oathbound.Plugin.Relay;

/// RFC 8785 (JSON Canonicalization Scheme) subset covering exactly the shapes the relay protocol uses:
/// strings, non-negative integers, booleans, nested objects (as IDictionary&lt;string, object?&gt;), and
/// arrays. The Worker's TypeScript side uses the `canonicalize` npm package (a full RFC 8785
/// implementation); this must byte-for-byte agree with it for every envelope this plugin ever signs or
/// verifies, so protocol/vectors/crypto-vectors.json is the source of truth this is tested against, not the
/// other way around. Object key order at every call site is irrelevant - keys are always sorted here by
/// ordinal (UTF-16 code unit) comparison, matching JavaScript's own string ordering for the ASCII-only field
/// names this protocol uses.
public static class CanonicalJson
{
    public static string Serialize(object? value)
    {
        var sb = new StringBuilder();
        Write(value, sb);
        return sb.ToString();
    }

    private static void Write(object? value, StringBuilder sb)
    {
        switch (value)
        {
            case null:
                sb.Append("null");
                break;
            case bool b:
                sb.Append(b ? "true" : "false");
                break;
            case string s:
                WriteString(s, sb);
                break;
            case int or long or short or byte or uint or ulong:
                sb.Append(Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
                break;
            case IDictionary<string, object?> dict:
                WriteObject(dict, sb);
                break;
            case IEnumerable enumerable:
                WriteArray(enumerable, sb);
                break;
            default:
                throw new NotSupportedException($"CanonicalJson does not support serializing {value.GetType()}.");
        }
    }

    private static void WriteObject(IDictionary<string, object?> dict, StringBuilder sb)
    {
        sb.Append('{');
        var first = true;
        // Ordinal comparison matches JavaScript's default string comparison for our ASCII-only key names,
        // which is what RFC 8785 requires (comparison by UTF-16 code unit).
        foreach (var key in dict.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            if (!first) sb.Append(',');
            first = false;
            WriteString(key, sb);
            sb.Append(':');
            Write(dict[key], sb);
        }
        sb.Append('}');
    }

    private static void WriteArray(IEnumerable enumerable, StringBuilder sb)
    {
        sb.Append('[');
        var first = true;
        foreach (var item in enumerable)
        {
            if (!first) sb.Append(',');
            first = false;
            Write(item, sb);
        }
        sb.Append(']');
    }

    private static void WriteString(string s, StringBuilder sb)
    {
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        sb.Append(c); // Non-ASCII passes through literally, matching JCS and JS's JSON.stringify.
                    break;
            }
        }
        sb.Append('"');
    }
}
