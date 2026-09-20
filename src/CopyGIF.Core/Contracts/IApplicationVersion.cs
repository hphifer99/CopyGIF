namespace CopyGIF.Core.Contracts;

/// <summary>The version of the running application, for example "1.2.3.0".</summary>
public interface IApplicationVersion
{
    string Current { get; }
}
