using System.Reflection;

public partial class Program
{
    const string funcDllPathForTesting = "E:\\unity-projects\\project-8-server\\bin\\Release\\net6.0\\publish\\project-8-server.dll";

    static string funcDllPath;
    static Assembly funcDll;
    static Dictionary<string, object> dicCacheFunc = new Dictionary<string, object>();

    static void LoadFuncDll(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            var fullname = new AssemblyName(args.Name);
            var folder = Directory.GetParent(funcDllPath).FullName;
            var assemblyPath = Path.Combine(folder, $"{fullname.Name}.dll");
            return Assembly.LoadFile(assemblyPath);
        };

        if (args == null || args.Length == 0)
        {
            funcDllPath = funcDllPathForTesting;
        }
        else
        {
            funcDllPath = Path.Combine(Directory.GetCurrentDirectory(), $"{args[0]}.dll");
        }

        funcDll = Assembly.LoadFile(funcDllPath);
    }

    static object CreateContextObj(string playerId, Dictionary<string, string> envs)
    {
        var type = funcDll.GetType("LocalServerContext");
        var ctor = type.GetConstructor(new[] { typeof(string), typeof(Dictionary<string, string>) });
        return ctor.Invoke(new object[] { playerId, envs });
    }

    static object GetFuncObj(string typeName)
    {
        try
        {
            if (!dicCacheFunc.ContainsKey(typeName))
            {
                var type = funcDll.GetType(typeName);
                var obj = Activator.CreateInstance(type);
                dicCacheFunc.Add(typeName, obj);
            }
            return dicCacheFunc[typeName];
        }
        catch
        {
            return null;
        }
    }
}