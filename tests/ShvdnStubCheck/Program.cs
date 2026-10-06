using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Serialization;
using GTA.Math;

namespace ShvdnStubCheck
{
    // Same shape as RageCoop.Resources.Race.Objects.Map, which is what crashed.
    [XmlRoot(ElementName = "Race")]
    public class Map
    {
        [XmlArrayItem(ElementName = "Vector3")]
        public Vector3[] Checkpoints;
        public SpawnPoint[] SpawnPoints;
        public string Name;
    }

    public class SpawnPoint
    {
        public Vector3 Position;
        public float Heading;
    }

    internal static class Program
    {
        private const string MapXml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<Race>\n" +
            "\t<Checkpoints>\n" +
            "\t\t<Vector3><X>1167.5</X><Y>-2077.25</Y><Z>41.5</Z></Vector3>\n" +
            "\t</Checkpoints>\n" +
            "\t<SpawnPoints><SpawnPoint><Position><X>1</X><Y>2</Y><Z>3</Z></Position><Heading>90</Heading></SpawnPoint></SpawnPoints>\n" +
            "\t<Name>check</Name>\n" +
            "</Race>";

        private static int Main(string[] args)
        {
            var problems = new List<string>();
            try
            {
                Run(args, problems);
            }
            catch (Exception ex)
            {
                var inner = ex;
                while (inner.InnerException != null) inner = inner.InnerException;
                problems.Add(ex.GetType().Name + ": " + ex.Message + " ---> " + inner.GetType().Name + ": " + inner.Message);
            }

            if (problems.Count == 0)
            {
                Console.WriteLine("::notice title=ShvdnStubCheck::OK - GTA.Math types load and deserialize on " + Environment.Version);
                return 0;
            }

            foreach (var p in problems)
                Console.WriteLine("::error title=ShvdnStubCheck::" + p.Replace("\r", " ").Replace("\n", " "));
            return 1;
        }

        private static void Run(string[] args, List<string> problems)
        {
            // 1. The exact operation that failed on the server.
            var map = (Map)new XmlSerializer(typeof(Map)).Deserialize(new StringReader(MapXml));
            if (map.Checkpoints.Length != 1 || map.Checkpoints[0].X != 1167.5f || map.Checkpoints[0].Z != 41.5f)
                problems.Add("Map checkpoints did not round-trip");
            if (map.SpawnPoints[0].Position.Y != 2f)
                problems.Add("Map spawn point did not round-trip");

            // 2. Every public signature on the math types must resolve.
            var shvdn3 = typeof(Vector3).Assembly;
            foreach (var type in shvdn3.GetExportedTypes().Where(t => t.Namespace == "GTA.Math"))
            {
                const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
                foreach (var c in type.GetConstructors(all)) c.GetParameters();
                foreach (var m in type.GetMethods(all)) { m.GetParameters(); _ = m.ReturnType; }
                foreach (var f in type.GetFields(all)) _ = f.FieldType;
                foreach (var p in type.GetProperties(all)) _ = p.PropertyType;
            }

            // 3. What got loaded as "ScriptHookVDotNet" must be the stand-in, at the version ScriptHookVDotNet3 asks for.
            var wanted = shvdn3.GetReferencedAssemblies().First(a => a.Name == "ScriptHookVDotNet");
            var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "ScriptHookVDotNet");
            if (loaded == null)
                problems.Add("ScriptHookVDotNet was never loaded; the check is not exercising the stand-in");
            else
            {
                if (!loaded.GetCustomAttributes<AssemblyMetadataAttribute>().Any(a => a.Key == "RageCoop.ShvdnStub"))
                    problems.Add("Loaded ScriptHookVDotNet is not the RageCoop stand-in: " + loaded.Location);
                if (loaded.GetName().Version != wanted.Version)
                    problems.Add("Stand-in version " + loaded.GetName().Version + " does not match the " + wanted.Version + " that ScriptHookVDotNet3 references; update RageCoop.ShvdnStub.csproj");
            }

            // 4. Every type ScriptHookVDotNet3 references from ScriptHookVDotNet must exist in the stand-in.
            if (loaded != null)
                foreach (var name in ReferencedCoreTypes(shvdn3.Location))
                    if (loaded.GetType(name, false) == null)
                        problems.Add("Stand-in is missing type " + name + "; add it to RageCoop.ShvdnStub/Stub.cs");

            // 5. Optionally, the server output folder must ship the stand-in rather than the native DLL.
            if (args.Length > 0)
            {
                var shipped = Path.Combine(args[0], "ScriptHookVDotNet.dll");
                if (!File.Exists(shipped))
                    problems.Add("No ScriptHookVDotNet.dll in server output " + args[0]);
                else
                    using (var pe = new PEReader(File.OpenRead(shipped)))
                    {
                        if (pe.PEHeaders.CorHeader == null || (pe.PEHeaders.CorHeader.Flags & CorFlags.ILOnly) == 0)
                            problems.Add("Server output ships the native ScriptHookVDotNet.dll instead of the stand-in: " + shipped);
                    }
            }
        }

        private static IEnumerable<string> ReferencedCoreTypes(string shvdn3Path)
        {
            using (var pe = new PEReader(File.OpenRead(shvdn3Path)))
            {
                var md = pe.GetMetadataReader();
                var result = new List<string>();
                foreach (var handle in md.TypeReferences)
                {
                    var name = FullName(md, handle, out var scope);
                    if (scope == "ScriptHookVDotNet") result.Add(name);
                }
                return result;
            }
        }

        private static string FullName(MetadataReader md, TypeReferenceHandle handle, out string scope)
        {
            var tr = md.GetTypeReference(handle);
            var name = md.GetString(tr.Name);
            if (tr.ResolutionScope.Kind == HandleKind.TypeReference)
                return FullName(md, (TypeReferenceHandle)tr.ResolutionScope, out scope) + "+" + name;
            scope = tr.ResolutionScope.Kind == HandleKind.AssemblyReference
                ? md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)tr.ResolutionScope).Name)
                : null;
            var ns = md.GetString(tr.Namespace);
            return ns.Length == 0 ? name : ns + "." + name;
        }
    }
}
