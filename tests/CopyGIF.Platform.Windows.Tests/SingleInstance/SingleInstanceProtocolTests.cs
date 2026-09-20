using System.Buffers.Binary;
using CopyGIF.Platform.Windows.SingleInstance;

namespace CopyGIF.Platform.Windows.Tests.SingleInstance;

[TestClass]
public sealed class SingleInstanceProtocolTests
{
    [TestMethod]
    public async Task WriteThenReadArgumentsAsync_RoundTripsArguments()
    {
        string[] expected =
        {
            "--open",
            "settings",
            "value with spaces"
        };

        await using MemoryStream stream =
            new();

        await SingleInstanceProtocol
            .WriteArgumentsAsync(
                stream,
                expected);

        stream.Position = 0;

        IReadOnlyList<string> actual =
            await SingleInstanceProtocol
                .ReadArgumentsAsync(stream);

        CollectionAssert.AreEqual(
            expected,
            actual.ToArray());
    }

    [TestMethod]
    public async Task ReadArgumentsAsync_OversizedLength_IsRejectedBeforeAllocation()
    {
        byte[] header =
            new byte[sizeof(int)];

        BinaryPrimitives.WriteInt32LittleEndian(
            header,
            65537);

        await using MemoryStream stream =
            new(header);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () =>
                SingleInstanceProtocol
                    .ReadArgumentsAsync(stream));
    }

    [TestMethod]
    public async Task WriteArgumentsAsync_TooManyArguments_IsRejected()
    {
        string[] arguments =
            Enumerable.Repeat(
                    "argument",
                    65)
                .ToArray();

        await using MemoryStream stream =
            new();

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () =>
                SingleInstanceProtocol
                    .WriteArgumentsAsync(
                        stream,
                        arguments));
    }

    [TestMethod]
    public async Task WriteArgumentsAsync_OverlongArgument_IsRejected()
    {
        string[] arguments =
        {
            new('x', 4097)
        };

        await using MemoryStream stream =
            new();

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () =>
                SingleInstanceProtocol
                    .WriteArgumentsAsync(
                        stream,
                        arguments));
    }
}
