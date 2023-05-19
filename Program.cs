using LitJson;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SymbolCompiler;

internal class Program
{
    readonly static Dictionary<int, string> stringliteral_dict = new();
    readonly static Dictionary<int, string> script_string_dict = new();
    readonly static Dictionary<int, string> method_dict = new();
    readonly static Dictionary<int, string> metadata_dict = new();
    readonly static Dictionary<int, (string, int)> metadata_method_dict = new();
    readonly static List<int> addresses_values = new();
    
    static bool GetSymbols(int offset, StringBuilder builder)
    {
        var any = false;
        if(stringliteral_dict.TryGetValue(offset, out var symbol))
        {
            builder.AppendLine(symbol);
            any = true;
        }
        if (script_string_dict.TryGetValue(offset, out var script))
        {
            builder.AppendLine(script);
            any = true;
        }
        if (method_dict.TryGetValue(offset, out var method))
        {
            builder.AppendLine(method);
            any = true;
        }
        if (metadata_dict.TryGetValue(offset, out var metadata))
        {
            builder.AppendLine(metadata);
            any = true;
        }
        if (metadata_method_dict.TryGetValue(offset, out var metadata_method))
        {
            builder.AppendLine($"{metadata_method.Item1}:{metadata_method.Item2}");
            any = true;
        }

        return any;
    }

    enum SymbolType:int
    {
        None = 0,
        String,
        Method,
        OtherData,
    }
    static void LoadJson(string script_file,string stringliteral_file)
    {
        var script_json = JsonMapper.ToObject(File.ReadAllText(script_file));
        var stringliteral_json = JsonMapper.ToObject(File.ReadAllText(stringliteral_file));
        var script_method = script_json["ScriptMethod"];
        var script_string = script_json["ScriptString"];
        var script_metadata = script_json["ScriptMetadata"];
        var script_metadata_method = script_json["ScriptMetadataMethod"];
        var addresses = script_json["Addresses"];

        foreach (JsonData literal in stringliteral_json)
        {
            JsonData address = literal["address"];
            JsonData value = literal["value"];
            var va = int.TryParse(address.ToString(),
                System.Globalization.NumberStyles.HexNumber
                | System.Globalization.NumberStyles.AllowHexSpecifier, null, out var a) ? a : 0;
            stringliteral_dict[va] = (string)value;
        }
        foreach (JsonData script in script_string)
        {
            JsonData address = script["Address"];
            JsonData value = script["Value"];
            var va = int.TryParse(address.ToString(),
                System.Globalization.NumberStyles.HexNumber
                | System.Globalization.NumberStyles.AllowHexSpecifier, null, out var a) ? a : 0;

            script_string_dict[va] = (string)value;
        }

        foreach (JsonData method in script_method)
        {
            JsonData address = method["Address"];
            JsonData name = method["Name"];
            JsonData signature = method["Signature"];
            JsonData typesignature = method["TypeSignature"];

            var mi = $"{name}-{signature}-{typesignature}";
            method_dict[(int)address] = mi;
        }
        foreach (JsonData metadata in script_metadata)
        {
            JsonData address = metadata["Address"];
            JsonData name = metadata["Name"];
            JsonData signature = metadata["Signature"];

            var mi = $"{name}-{signature}";
            metadata_dict[(int)address] = mi;
        }
        foreach (JsonData metadata in script_metadata_method)
        {
            JsonData address = metadata["Address"];
            JsonData name = metadata["Name"];
            JsonData methodaddress = metadata["MethodAddress"];
            metadata_method_dict[(int)address] = ((string)name, (int)methodaddress);
        }
        foreach (JsonData v in addresses)
        {
            addresses_values.Add((int)v);
        }

    }
    static readonly string[] Prefixes =
    {
        "off_",
        "byte_",
        "word_",
        "dword_",
        "qword_",
    };

    static string GetDataName(int offset,string name)
    {
        return metadata_dict.TryGetValue(offset,out var data) ? data : name;
    }
    static string GetDataName(string name)
    {
        var offset = 0;
        var m = data_name.Match(name);
        if (m.Success && m.Groups.Count==2)
        {
            if (int.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out offset))
            {
                return GetDataName(offset, name);
            }
        }
        return name;
    }
    static string GetSubName(int offset,string functionName)
    {
        return method_dict.TryGetValue(offset, out var data) ? data :
            metadata_method_dict.TryGetValue(offset,out var data2)?data2.Item1: functionName;
    }
    static string GetSubName(string name)
    {
        var offset = 0;
        var m = sub_name.Match(name);
        if (m.Success && m.Groups.Count==2)
        {
            if (int.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out offset))
            {
                return GetSubName(offset, name);
            }
        }
        return name;
    }
    static string ReplaceNames(string text)
    {
        text = data_name.Replace(text, 
            (n) => { 
                return GetDataName(n.Value); 
            }
            );

        text = sub_name.Replace(text,
            (n) => { 
                return GetSubName(n.Value); 
            }
            );

        return text;
    }

    static Regex dcx = new ("DC[BWDQ]");
    static Regex data_name = new("(off_|byte_|word_|dword_|qword_)([0-9a-fA-F]{1,8})");
    static Regex sub_name = new("sub_([0-9a-fA-F]{1,8})");
    static int Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("SymbolCompiler script.json stringliteral.json libil2cpp.lst");
            return -1;
        }

        LoadJson(args[0], args[1]);
        var name_dict = new Dictionary<string, string>();

        var il2cpp_file = args[2];
        var il2cpp_changed_file 
            = Path.ChangeExtension(il2cpp_file, ".compiled.lst");
        var current_sub = "";
        var current_function = "";

        var insub = false;
        using var reader = new StreamReader(il2cpp_file);
        using var writer = new StreamWriter(il2cpp_changed_file);
        string? line = null;
        var lineno = 0;
        var lastcp = 0;
        while(null!=(line = reader.ReadLine()))
        {
            lineno++;
            var builder = new StringBuilder();
            if (line.Length > 0)
            {
                var segment = "";
                int offset = 0;
                var comment = "";
                var address = "";
                var p = line.IndexOf(' ');
                if (p >= 0 || p == -1)
                {
                    if ((address = p == -1 ? line : line[..p]).Length > 0
                        && (p = address.IndexOf(':')) >= 0)
                    {
                        segment = address[..p];
                        var of = address[(p + 1)..];
                        int.TryParse(of, out offset);
                    }
                    line = line[address.Length..].Trim();
                }
                if ((p = line.IndexOf(';')) >= 0)
                {
                    comment = line[p..];
                    line = line[0..p];
                }
                //Header
                var _line = line.Replace(", ", ",");
                if(lineno == 41)
                {

                }
                //Parts
                var parts = _line.Split(' ',StringSplitOptions.RemoveEmptyEntries);
                //data definition
                if (parts.Length >= 3 && dcx.IsMatch(parts[1].Trim()))
                {
                    var n = parts[0];
                    var match = data_name.Match(n);
                    if(match.Success&&match.Groups.Count>0)
                    {
                        //Find the name and replace it
                        //and keep it dict
                        var r = GetDataName(offset, n);
                        name_dict[n] = parts[0] = r;
                    }
                    if(line.StartsWith("DCB"))
                    {

                    }
                    {
                        parts[0] = parts[0].PadRight(16);
                        parts[1] = parts[1].PadRight(4);
                        parts[2] = parts[2].PadRight(20);
                    }

                    line = string.Join("", parts);
                }
                else if(parts.Length == 1 && parts[0].Length>0)
                {
                    var mcs = sub_name.Match(parts[0]);
                    if (mcs.Success && mcs.Groups.Count==2)
                    {
                        var ofs = mcs.Groups[1].Value;

                        if (int.TryParse(ofs, System.Globalization.NumberStyles.HexNumber, null, out var ofn))
                        {
                            current_function = GetSubName(ofn, current_sub = parts[0]);
                            insub = true;
                            if (!current_function.StartsWith("sub_"))
                            {
                                line = current_function;
                            }
                        }
                    }
                }
                else if (parts.Length == 0)
                {
                    var comment_v = $"; End of function {current_sub}";
                    if(comment == comment_v)
                    {
                        insub = false; 
                        comment = $"; End of function {current_function}";
                        current_sub = "";//quit subs
                    }
                }
                else if(insub)
                {
                    if ((p = line.IndexOf(' ')) >= 0)
                        while (p < line.Length && line[p] == ' ') p++;
                    line = line[..p] + ReplaceNames(line[p..]);
                }
                //if (comment.StartsWith(';'))
                //{
                //    comment = new string(' ', 39) + comment;
                //}
                comment = ReplaceNames(comment);
                if (line.Length == 0)
                {
                    comment = new string(' ', lastcp) + comment;
                }
                lastcp = line.Length>0?line.Length:lastcp;
                line = $"{segment}:{offset:X8} {line}{comment}";
            }
            Console.WriteLine($"{lineno} {line}");
            writer.WriteLine(line);
        }
        return 0;
    }
}