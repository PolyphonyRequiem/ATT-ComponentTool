// See https://aka.ms/new-console-template for more information

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;

Console.WriteLine("Welcome to ATT Component Tool.");

string gameAssemblyPath;

if (args.Length > 0)
{
    Console.WriteLine($"Attempting to load the game library from {args[1]}");
    gameAssemblyPath = args[1];
}
else
{
    Console.WriteLine($"Attempting to load the game library from the default path as no arguments were provided.");
    gameAssemblyPath = @"C:\Games\Alta\A Township Tale\A Township Tale_Data\Managed\";
}

var loadContext = new CustomAssemblyLoadContext(gameAssemblyPath);
Assembly rootAssembly = loadContext.LoadFromAssemblyName(new AssemblyName("Root.Township"));

foreach (var module in rootAssembly.GetModules())
{
    Type?[] moduleTypes;
    try
    {
        moduleTypes = module.GetTypes();
    }
    catch (ReflectionTypeLoadException e)
    {
        // Just get the types that were successfully loaded.
        moduleTypes = e.Types.Where(t => t != null).ToArray();
    }

    foreach (Type? type in moduleTypes)
    {
        if (type == null)
        {
            continue;
        }

        try
        {
            foreach (var attribute in type.GetCustomAttributes(inherit: false))
            {
                if (attribute.GetType().Name == "SaveStructureAttribute")
                {
                    uint attributeVersion = (uint) attribute.GetType().GetProperty("Version")?.GetValue(attribute)!;

                    Console.WriteLine($"// Version={attributeVersion}");
                    DumpType(type);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to process type {type.FullName}: {e}");
        }
    }
}

void DumpType(Type type, string indent = "", string parentName = "")
{
    StringBuilder typeOutput = new StringBuilder();

    if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
    {
        string tsType = GetTsType(type);
        typeOutput.AppendLine($"{indent}{parentName}: {tsType};");
    }
    else if (!type.IsSubclassOf(typeof(MulticastDelegate)) && !type.IsGenericType)
    {
        typeOutput.AppendLine($"{indent}interface {type.Name} {{");

        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (!field.IsDefined(typeof(CompilerGeneratedAttribute), false))
            {
                string tsType = GetTsType(field.FieldType);
                typeOutput.AppendLine($"{indent}  {field.Name}: {tsType};");
            }
        }

        foreach (PropertyInfo prop in type.GetProperties())
        {
            string tsType = GetTsType(prop.PropertyType);
            typeOutput.AppendLine($"{indent}  {prop.Name}: {tsType};");
        }

        typeOutput.AppendLine($"{indent}}}\n");
    }
    string output = typeOutput.ToString();

    Console.WriteLine(output);
    if (!Directory.Exists("./out"))
    {
        Directory.CreateDirectory("./out");
    }

    File.WriteAllText($"./out/{type.Name}.ts", output);
}

string GetTsType(Type type)
{
    if (type.IsPrimitive)
    {
        if (type == typeof(int) || type == typeof(float) || type == typeof(double) || type == typeof(decimal) || type == typeof(uint))
        {
            return "number";
        }
        else if (type == typeof(bool))
        {
            return "boolean";
        }
        else if (type == typeof(char) || type == typeof(string))
        {
            return "string";
        }
        else // Other primitive types
        {
            return "any";
        }
    }
    else if (type == typeof(string))
    {
        return "string";
    }
    else if (type.IsEnum)
    {
        return "string";  // Assuming you want to represent enums as strings
    }
    else // Non-primitive, non-string, non-enum types
    {
        return type.Name; // Just use the type's name
    }
}


public class CustomAssemblyLoadContext : AssemblyLoadContext
{
    private string assemblyPath;

    public CustomAssemblyLoadContext(string assemblyPath)
    {
        this.assemblyPath = assemblyPath;
    }

    protected override Assembly Load(AssemblyName assemblyName)
    {
        // Attempt to load the assembly using the default AssemblyLoadContext.
        try
        {
            Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
            return assembly;
        }
        catch (Exception)
        {
            // Ignore any exceptions.
        }

        // If the assembly could not be loaded using the default AssemblyLoadContext,
        // try to load it from the specified directory.
        string assemblyFile = Path.Combine(assemblyPath, $"{assemblyName.Name}.dll");

        if (File.Exists(assemblyFile))
        {
            return LoadFromAssemblyPath(assemblyFile);
        }

        throw new Exception($"Could not load assembly {assemblyName.Name} from {assemblyFile}");
    }
}
