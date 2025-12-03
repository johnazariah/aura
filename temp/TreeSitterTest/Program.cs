using System.Reflection;
var assembly = Assembly.LoadFrom(@"C:\Users\johnaz\.nuget\packages\treesitter.bindings\0.4.0\lib\net8.0\TreeSitter.Bindings.dll");
foreach (var type in assembly.GetExportedTypes().OrderBy(t => t.FullName))
{
    Console.WriteLine($"Type: {type.FullName}");
    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Take(5))
    {
        Console.WriteLine($"  - {method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})");
    }
}
