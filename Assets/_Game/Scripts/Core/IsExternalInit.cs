// Records compile to init-only setters, which the compiler marks with this type. .NET Standard 2.1 predates it and
// none of Unity's reference assemblies add it, so Core (which references no engine assembly) declares its own.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
