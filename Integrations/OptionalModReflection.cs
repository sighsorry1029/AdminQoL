using System;
using System.Linq;
using System.Reflection;

namespace AdminQoL;

internal static class OptionalModReflection
{
    internal const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
    internal const BindingFlags NonPublicStatic = BindingFlags.NonPublic | BindingFlags.Static;
    internal const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

    internal static Assembly? FindAssembly(string assemblyName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
    }

    internal static Type? GetType(Assembly? assembly, string typeName)
    {
        return assembly?.GetType(typeName);
    }

    internal static MethodInfo? GetPublicStaticMethod(Type? type, string methodName)
    {
        return type?.GetMethod(methodName, PublicStatic);
    }

    internal static FieldInfo? GetPublicStaticField(Type? type, string fieldName)
    {
        return type?.GetField(fieldName, PublicStatic);
    }

    internal static Exception UnwrapInvocation(Exception ex)
    {
        return ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
    }
}
