using LitJson;
using System.Text;
using System.Text.RegularExpressions;

namespace SymbolCompiler;

internal class Program
{
    readonly static SortedDictionary<int, string> stringliteral_dict = new();
    readonly static SortedDictionary<int, string> script_string_dict = new();
    readonly static SortedDictionary<int, string> metadata_dict = new();
    readonly static SortedDictionary<int, string> method_dict = new();
    readonly static SortedDictionary<int, (string, int)> metadata_method_dict = new();
    readonly static List<int> addresses_values = new();

    enum SymbolType : int
    {
        None = 0,
        String,
        Method,
        OtherData,
    }
    static void LoadJson(string script_file, string stringliteral_file)
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

            var mi = $"{signature}";
            method_dict[(int)address] = mi;
        }
        foreach (JsonData metadata in script_metadata)
        {
            JsonData address = metadata["Address"];
            JsonData name = metadata["Name"];
            JsonData signature = metadata["Signature"];

            var mi = $"{signature}";
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

    static string GetDataName(int offset, string name)
    {
        return metadata_dict.TryGetValue(offset, out var data) ? data : name;
    }
    static string GetDataName(string name)
    {
        var offset = 0;
        var m = data_name.Match(name);
        if (m.Success && m.Groups.Count == 2)
        {
            if (int.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out offset))
            {
                return GetDataName(offset, name);
            }
        }
        return name;
    }
    static string GetSubName(int offset, string functionName)
    {
        return method_dict.TryGetValue(offset, out var data) ? data :
            metadata_method_dict.TryGetValue(offset, out var data2) ? data2.Item1 : functionName;
    }
    static string GetSubName(string name)
    {
        var offset = 0;
        var m = sub_name.Match(name);
        if (m.Success && m.Groups.Count == 2)
        {
            if (int.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out offset))
            {
                return GetSubName(offset, name);
            }
        }
        return name;
    }
    static string ReplaceNames(string text)
        => sub_name.Replace(
             data_name.Replace(text,
               (n) => GetDataName(n.Value)),
               (n) => GetSubName(n.Value));

    static Regex module_name = new("Module\\(0x([0-9a-fA-F]{1,8}),0x([0-9a-fA-F]{1,8})\\)\\:(.*)");
    static Regex dcx = new("DC[BWDQ]");
    static Regex data_name = new("(off_|byte_|word_|dword_|qword_)([0-9a-fA-F]{1,8})");
    static Regex sub_name = new("sub_([0-9a-fA-F]{1,8})");
    //script.json stringliteral.json C:\Working\DouluoDalu\libmain\libil2cpp-dump-64\libil2cpp-64.lst
    static void ProcessListFile(string il2cpp_list_file, string il2cpp_list_compiled_file)
    {
        var current_sub = "";
        var current_function = "";

        var insub = false;
        using var reader = new StreamReader(il2cpp_list_file);
        using var writer = new StreamWriter(il2cpp_list_compiled_file);
        string? line = null;
        var lineno = 0;
        var lastcp = 0;
        while (null != (line = reader.ReadLine()))
        {
            lineno++;
            var builder = new StringBuilder();
            var useful = false;
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
                        if (!int.TryParse(of,
                            System.Globalization.NumberStyles.HexNumber, null,
                            out offset))
                        {

                        }
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

                //Parts
                var parts = _line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                //data definition
                if (parts.Length >= 2 && dcx.IsMatch(parts[0]))
                {
                    parts[0] = new string(' ', 16) + parts[0].PadRight(4);
                    parts[1] = parts[1].PadRight(20);

                    line = string.Join("", parts);
                    useful = true;
                }
                else if (parts.Length >= 3 && dcx.IsMatch(parts[1].Trim()))
                {
                    var n = parts[0];
                    var match = data_name.Match(n);
                    if (match.Success && match.Groups.Count > 0)
                    {
                        //Find the name and replace it
                        //and keep it dict
                        var r = GetDataName(offset, n);
                        parts[0] = r;
                    }

                    {
                        parts[0] = parts[0].PadRight(16);
                        parts[1] = parts[1].PadRight(4);
                        parts[2] = parts[2].PadRight(20);
                    }

                    line = string.Join("", parts);
                    useful = true;
                }
                else if (parts.Length == 1 && parts[0].Length > 0)
                {
                    var mcs = sub_name.Match(parts[0]);
                    if (mcs.Success && mcs.Groups.Count == 2)
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
                        useful = true;
                        line += Environment.NewLine;
                        line += $"{segment}:{offset:X8} {{";
                    }

                }
                else if (parts.Length == 0)
                {
                    var comment_v = $"; End of function {current_sub}";
                    if (comment == comment_v)
                    {
                        insub = false;
                        line = "}";
                        comment = $"; End of function {current_function}";
                        current_sub = "";//quit subs
                        useful = true;
                    }

                }
                else if (insub)
                {
                    if ((p = line.IndexOf(' ')) >= 0)
                        while (p < line.Length && line[p] == ' ') p++;
                    line = new string(' ', 4) + line[..p] + ReplaceNames(line[p..]);
                    useful = true;
                }

                comment = ReplaceNames(comment);
                if (line.Length == 0)
                {
                    comment = new string(' ', lastcp) + comment;
                }
                lastcp = line.Length > 0 ? line.Length : lastcp;
                line = $"{segment}:{offset:X8} {line}{comment}";
            }
            //Console.WriteLine($"{lineno} {line}");
            if (useful)
            {
                writer.WriteLine(line);
            }
        }

    }

    //script.json stringliteral.json C:\Working\DouluoDalu\libmain\libil2cpp-dump-64\libil2cpp-64.c
    static void ProcessCFile(string il2cpp_c_file, string il2cpp_c_compiled_file)
    {
        var lineno = 0;
        var changes = 0;
        using var reader = new StreamReader(il2cpp_c_file);
        using var writer = new StreamWriter(il2cpp_c_compiled_file);
        string? line = null;
        while (null != (line = reader.ReadLine()))
        {
            lineno++;
            var outline = ReplaceNames(line);
            if (line != outline) changes++;
            writer.WriteLine(outline);
        }
    }
    static (int, int) GetStartAddress(int rva)
        => GetStartAddressWithArray(rva,
            metadata_method_dict.Keys.ToArray(),
            method_dict.Keys.ToArray());

    static (int, int) GetStartAddressWithArray(int rva, params int[][] addressesArray)
    {
        foreach (var addresses in addressesArray)
        {
            for (int i = 0; i < addresses.Length; i++)
            {
                int pre = addresses[i];
                if (i < addresses.Length - 1)
                {
                    int post = addresses[i + 1];
                    if (rva >= pre && rva < post)
                    {
                        return (pre, rva - pre);
                    }
                }
                else if (i == addresses.Length - 1)
                {
                    if (rva >= pre)
                    {
                        return (pre, rva - pre);
                    }
                }
            }
        }

        return (0, 0);
    }
    //script.json stringliteral.json C:\Working\DouluoDalu\libmain\libil2cpp-dump-64\stack_libtolua.txt
    static void ProcessStackDumpFile(string stack_dump_file, string stack_dump_compiled_file)
    {
        //STACK: #0015:0x768dd51ea8,0x56ac5ea8 |  | /data/app/com.sy.dldlhsdj.gw--STmIOAipmAEG8nRidJ1VA==/lib/arm64/libil2cpp.so
        using var reader = new StreamReader(stack_dump_file);
        using var writer = new StreamWriter(stack_dump_compiled_file);
        string? line = null;
        var lineno = 0;
        var modules = new Dictionary<(long, long), string>();
        while (null != (line = reader.ReadLine()))
        {
            lineno++;
            line = line.Trim();
            if (line.Length == 0) continue;
            var m = module_name.Match(line);
            if (m.Success && m.Groups.Count == 4)
            {
                var start_text = m.Groups[1].Value;
                var end_text = m.Groups[2].Value;
                var name_text = m.Groups[3].Value;
                if (long.TryParse(start_text, System.Globalization.NumberStyles.HexNumber, null, out var start)
                    && long.TryParse(end_text, System.Globalization.NumberStyles.HexNumber, null, out var end))
                {
                    modules.Add((start, end), name_text);
                }
            }
            else if (line.StartsWith("STACK: #"))
            {
                var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 && parts[1].Trim().Length == 0)
                {
                    int p = parts[0].LastIndexOf(" = ");
                    if (p >= 0)
                    {
                        var rva_text = parts[0][(p + 3)..];
                        if ((rva_text.StartsWith("0x") || rva_text.StartsWith("0X"))
                            && int.TryParse(rva_text[2..],
                            System.Globalization.NumberStyles.AllowHexSpecifier
                            | System.Globalization.NumberStyles.HexNumber, null, out var rva))
                        {
                            var (start, offset) = GetStartAddress(rva);
                            parts[1] = start != 0 ? $" {GetSubName(start, $"sub_{start:X8}")}({start:X8}) + {offset:X8}" : $" {GetSubName(rva, $"sub_{rva:X8}")} ";
                            line = string.Join('|', parts);
                        }
                    }
                }
            }
            writer.WriteLine(line);
        }
    }

    static void ProcessDirectoryDumpFIle(string directory_to_dump, string directory_to_dump_file, string pattern = "*.bytes", string prefix = "@")
    {
        using var writer = new StreamWriter(directory_to_dump_file);
        var files = Directory.GetFiles(directory_to_dump, pattern, SearchOption.AllDirectories);
        if (!directory_to_dump.EndsWith(Path.DirectorySeparatorChar))
        {
            directory_to_dump += Path.DirectorySeparatorChar;
        }
        foreach (var file in files)
        {
            var name = file[directory_to_dump.Length..(file.Length - Path.GetExtension(file).Length)];
            if (name.ToLower().StartsWith("lua\\"))
            {
                name = name["lua\\".Length..];
            }
            else if (name.ToLower().StartsWith("sourcelua\\"))
            {
                name = name["sourcelua\\".Length..];
            }
            name = prefix + name.Replace(Path.DirectorySeparatorChar, '/');
            writer.WriteLine(name);
        }
    }
    static int Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("SymbolCompiler script.json stringliteral.json libil2cpp.lst");
            return -1;
        }

        LoadJson(args[0], args[1]);

        if (Path.GetExtension(args[2]).ToLower() == ".lst")
        {
            var il2cpp_list_file = args[2];
            var il2cpp_list_compiled_file
                = Path.ChangeExtension(il2cpp_list_file, ".compiled.lst");

            ProcessListFile(il2cpp_list_file, il2cpp_list_compiled_file);
        }
        else if (Path.GetExtension(args[2]).ToLower() == ".c")
        {
            var il2cpp_c_file = args[2];
            var il2cpp_c_compiled_file
                = Path.ChangeExtension(il2cpp_c_file, ".compiled.c");
            ProcessCFile(il2cpp_c_file, il2cpp_c_compiled_file);
        }
        else if (Path.GetExtension(args[2]).ToLower() == ".txt")
        {
            var stack_dump_file = args[2];
            var stack_dump_compiled_file
                = Path.ChangeExtension(stack_dump_file, ".compiled.txt");
            ProcessStackDumpFile(stack_dump_file, stack_dump_compiled_file);
        }
        else if (Directory.Exists(args[2])) //if it is directory
        {
            var directory_to_dump = args[2];
            var directory_name = Path.GetFileName(directory_to_dump) ?? "";
            var parent_name = Path.GetDirectoryName(directory_to_dump);
            var directory_to_dump_file = Path.Combine(parent_name, directory_name
                 + ".compiled.txt");
            ProcessDirectoryDumpFIle(directory_to_dump, directory_to_dump_file);
        }


        return 0;
    }
}