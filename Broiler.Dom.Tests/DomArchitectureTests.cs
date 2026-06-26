using System.Reflection;

namespace Broiler.Dom.Tests;

public sealed class DomArchitectureTests
{
    [Fact]
    public void Kernel_Has_No_NonFramework_Assembly_Dependencies()
    {
        var dependencies = typeof(DomDocument).Assembly
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name)
            .Where(static name =>
                name is not null &&
                !name.StartsWith("System", StringComparison.Ordinal) &&
                !string.Equals(name, "mscorlib", StringComparison.Ordinal) &&
                !string.Equals(name, "netstandard", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(dependencies);
    }

    [Fact]
    public void Public_Surface_Does_Not_Leak_Forbidden_Broiler_Types()
    {
        var leaks = typeof(DomDocument).Assembly
            .GetExportedTypes()
            .SelectMany(static type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .SelectMany(GetMemberTypes)
            .Where(static type =>
                type.Namespace?.StartsWith("Broiler.", StringComparison.Ordinal) == true &&
                type.Namespace != "Broiler.Dom")
            .Select(static type => type.FullName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(leaks);
    }

    [Fact]
    public void Mutable_Collections_Are_Not_Publicly_Exposed()
    {
        var leaks = typeof(DomDocument).Assembly
            .GetExportedTypes()
            .SelectMany(static type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(static property =>
                property.PropertyType.IsGenericType &&
                property.PropertyType.GetGenericTypeDefinition() is var definition &&
                (definition == typeof(List<>) ||
                 definition == typeof(Dictionary<,>) ||
                 definition == typeof(HashSet<>)))
            .Select(static property => $"{property.DeclaringType!.Name}.{property.Name}")
            .ToArray();

        Assert.Empty(leaks);
    }

    private static IEnumerable<Type> GetMemberTypes(MemberInfo member) => member switch
    {
        MethodInfo method => [method.ReturnType, .. method.GetParameters().Select(static parameter => parameter.ParameterType)],
        PropertyInfo property => [property.PropertyType],
        FieldInfo field => [field.FieldType],
        EventInfo eventInfo when eventInfo.EventHandlerType is not null => [eventInfo.EventHandlerType],
        _ => [],
    };
}
