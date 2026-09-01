using System.Diagnostics.CodeAnalysis;

namespace CopyGIF.Core.Settings;

public enum ProviderDisplayMode
{
    [SuppressMessage(
        "Naming",
        "CA1720:Identifier contains type name",
        Justification =
            "Single is the intentional product term for displaying one provider.")]
    Single,

    SideBySide,

    Combined
}
