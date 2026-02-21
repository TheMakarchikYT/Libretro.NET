using System;
using System.Diagnostics;

namespace Libretro.NET.Bindings
{
    /// <summary>
    /// Native bindings were generated using the following command:
    /// <code>
    /// ClangSharpPInvokeGenerator -f libretro.h -n Libretro.NET.Bindings -o RetroBindings.cs -m RetroBindings -p retro_ -l core -c compatible-codegen unix-types generate-macro-bindings
    /// </code> 
    /// </summary>
    [Conditional("DEBUG")]
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue, AllowMultiple = false, Inherited = true)]
    internal sealed class NativeTypeNameAttribute : Attribute
    {
        private readonly string _name;
        public string Name { get { return _name; } }

        public NativeTypeNameAttribute(string name)
        {
            _name = name;
        }
    }
}