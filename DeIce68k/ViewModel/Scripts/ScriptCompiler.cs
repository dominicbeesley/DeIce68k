using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace DeIce68k.ViewModel.Scripts
{
    public static class ScriptCompiler
    {
        public static ScriptBase Compile(DeIceAppModel app, string myCode, out IEnumerable<string> errorsR)
        {
            var errors = new List<string>();
            errorsR = errors;

            string code = @$"
using DeIce68k.ViewModel.Scripts;
using DeIce68k.ViewModel;

public class ThisScript : ScriptBase {{

    public override bool Execute() {{
        {myCode};
    }}  

    public ThisScript(DeIceAppModel app, string code) : base (app, code) {{}}

}}

";


            SyntaxTree synTree = CSharpSyntaxTree.ParseText(code);

            var refs = AppDomain.CurrentDomain.GetAssemblies()
                .Where(r => r.IsDynamic == false && !string.IsNullOrEmpty(r.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location)).ToArray();

            var comp = CSharpCompilation.Create(
                assemblyName: Path.GetRandomFileName(),
                syntaxTrees: new[] { synTree },
                references: refs,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                );

            using (var ms = new MemoryStream())
            {
                var result = comp.Emit(ms);

                errors.AddRange(
                    result.Diagnostics.Select(d => d.ToString())
                    );

                if (result.Success)
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    Assembly a = new CollectibleAssemblyLoadContext().LoadFromStream(ms);
                    var t = a.GetTypes().Where(t => t.IsAssignableTo(typeof(ScriptBase))).FirstOrDefault();
                    if (t == null)
                    {
                        errors.Add("Unexpected error: assembly doesn't contain compatible type");
                    }

                    return a.CreateInstance(
                        t.FullName, 
                        false, 
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod, 
                        null, 
                        new object[] { app, code },
                        null,
                        new object[] { }
                        ) as ScriptBase;
                }
            }


            return null;

        }
    }
}
