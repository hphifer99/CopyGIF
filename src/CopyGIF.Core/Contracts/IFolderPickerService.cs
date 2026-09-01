namespace CopyGIF.Core.Contracts;

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync(
        string? initialDirectory = null,
        CancellationToken cancellationToken = default);
}
