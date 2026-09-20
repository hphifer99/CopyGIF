using System.Reflection;
using CopyGIF.Core.Contracts;

namespace CopyGIF.App.Services;

/// <summary>The version of the running CopyGIF executable.</summary>
public sealed class EntryAssemblyVersion :
    IApplicationVersion
{
    public string Current =>
        Assembly
            .GetEntryAssembly()?
            .GetName()
            .Version?
            .ToString() ??
        "0.0.0";
}
