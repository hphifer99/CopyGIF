using System.Buffers.Binary;
using System.Text.Json;

namespace CopyGIF.Platform.Windows.SingleInstance;

internal static class SingleInstanceProtocol
{
    private const int MaximumArgumentCount = 64;
    private const int MaximumArgumentLength = 4096;
    private const int MaximumPayloadLength = 65536;
    private const byte Acknowledgement = 0x06;

    public static async Task WriteArgumentsAsync(
        Stream stream,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        string[] validatedArguments =
            ValidateArguments(arguments);

        byte[] payload =
            JsonSerializer.SerializeToUtf8Bytes(
                validatedArguments);

        if (payload.Length > MaximumPayloadLength)
        {
            throw new ArgumentException(
                "The activation request is too large.",
                nameof(arguments));
        }

        byte[] header =
            new byte[sizeof(int)];

        BinaryPrimitives.WriteInt32LittleEndian(
            header,
            payload.Length);

        await stream.WriteAsync(
                header,
                cancellationToken)
            .ConfigureAwait(false);

        await stream.WriteAsync(
                payload,
                cancellationToken)
            .ConfigureAwait(false);

        await stream.FlushAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<string>>
        ReadArgumentsAsync(
            Stream stream,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        byte[] header =
            new byte[sizeof(int)];

        await stream.ReadExactlyAsync(
                header,
                cancellationToken)
            .ConfigureAwait(false);

        int payloadLength =
            BinaryPrimitives.ReadInt32LittleEndian(
                header);

        if (payloadLength is <= 0 or > MaximumPayloadLength)
        {
            throw new InvalidDataException(
                "The activation request has an invalid length.");
        }

        byte[] payload =
            new byte[payloadLength];

        await stream.ReadExactlyAsync(
                payload,
                cancellationToken)
            .ConfigureAwait(false);

        string[]? arguments;

        try
        {
            arguments =
                JsonSerializer.Deserialize<string[]>(
                    payload);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The activation request is not valid JSON.",
                exception);
        }

        if (arguments is null)
        {
            throw new InvalidDataException(
                "The activation request is empty.");
        }

        try
        {
            return Array.AsReadOnly(
                ValidateArguments(arguments));
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "The activation request contains invalid arguments.",
                exception);
        }
    }

    public static async Task WriteAcknowledgementAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        await stream.WriteAsync(
                new byte[] { Acknowledgement },
                cancellationToken)
            .ConfigureAwait(false);

        await stream.FlushAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task ReadAcknowledgementAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        byte[] acknowledgement =
            new byte[1];

        await stream.ReadExactlyAsync(
                acknowledgement,
                cancellationToken)
            .ConfigureAwait(false);

        if (acknowledgement[0] != Acknowledgement)
        {
            throw new InvalidDataException(
                "The primary CopyGIF instance rejected the activation request.");
        }
    }

    private static string[] ValidateArguments(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count > MaximumArgumentCount)
        {
            throw new ArgumentException(
                $"An activation request cannot contain more than {MaximumArgumentCount} arguments.",
                nameof(arguments));
        }

        string[] validated =
            new string[arguments.Count];

        for (int index = 0;
             index < arguments.Count;
             index++)
        {
            string? argument =
                arguments[index];

            if (argument is null)
            {
                throw new ArgumentException(
                    "Activation arguments cannot contain null values.",
                    nameof(arguments));
            }

            if (argument.Length > MaximumArgumentLength)
            {
                throw new ArgumentException(
                    $"An activation argument cannot exceed {MaximumArgumentLength} characters.",
                    nameof(arguments));
            }

            validated[index] = argument;
        }

        return validated;
    }
}
