using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;
using NDesk.Options;

namespace TypeForwarder
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            string in1 = null;
            string in2 = null;
            string outPath = null;
            bool help = false;
            OptionSet options =
                new OptionSet
                {
                    {"in1=", "Assembly 1", val => in1 = val},
                    {"in2=", "Assembly 2", val => in2 = val},
                    {"out=", "Output file", val => outPath = val},
                    {"help|?|h", "Display help", val => help = val != null},
                };

            try
            {
                options.Parse(args);
            }
            catch (OptionException ex)
            {
                Console.WriteLine("TypeForwarder: {0}", ex.Message);
                ShowHelp(options);
                return;
            }

            if (help || in1 == null || in2 == null || outPath == null || !File.Exists(in1) ||
                !File.Exists(in2))
            {
                ShowHelp(options);
                return;
            }

            AssemblyDefinition from = AssemblyDefinition.ReadAssembly(in1);
            AssemblyDefinition to = AssemblyDefinition.ReadAssembly(in2);

            HashSet<string> fromTypes = new HashSet<string>(GetPublicTypeNames(from));
            using (StreamWriter sw = new StreamWriter(outPath))
            {
                foreach (string toType in GetPublicTypeNames(to).Where(name => !fromTypes.Contains(name)))
                {
                    sw.WriteLine("[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof({0}))]", toType);
                }
            }
        }

        private static IEnumerable<string> GetPublicTypeNames(AssemblyDefinition def)
        {
            foreach (ModuleDefinition module in def.Modules)
            {
                foreach (TypeDefinition type in module.Types)
                {
                    if (!type.IsPublic)
                        continue;

                    yield return CSharpifyTypeName(type.FullName);
                }
            }
        }

        private static string CSharpifyTypeName(string name)
        {
            StringBuilder csharpName = new StringBuilder();

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c == '`')
                {
                    int numParams = 0;
                    for (i++; i < name.Length; i++)
                    {
                        char c2 = name[i];
                        if (c2 < '0' || c2 > '9')
                            break;

                        numParams *= 10;
                        numParams += c2 - '0';
                    }

                    i--;
                    csharpName.AppendFormat("<{0}>", new string(',', numParams - 1));
                    continue;
                }

                csharpName.Append(c);
            }

            return csharpName.ToString();
        }

        private static void ShowHelp(OptionSet options)
        {
            Console.WriteLine("Usage: TypeForwarder <options>");
            options.WriteOptionDescriptions(Console.Out);
            Console.WriteLine(
                "Generate type forwarders for types that are present in 'in2' but missing in 'in1'");
        }
    }
}
