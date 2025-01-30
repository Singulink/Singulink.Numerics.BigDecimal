namespace System.Diagnostics.CodeAnalysis;

#if NETSTANDARD

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
internal sealed class DoesNotReturnAttribute : Attribute
{
    public DoesNotReturnAttribute() { }
}

#endif