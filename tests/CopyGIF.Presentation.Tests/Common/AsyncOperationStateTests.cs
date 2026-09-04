using CopyGIF.Presentation.Common;

namespace CopyGIF.Presentation.Tests.Common;

[TestClass]
public sealed class AsyncOperationStateTests
{
    [TestMethod]
    public void Idle_HasExpectedState()
    {
        AsyncOperationState state =
            AsyncOperationState.Idle;

        Assert.AreEqual(
            AsyncOperationStatus.Idle,
            state.Status);

        Assert.IsFalse(
            state.IsBusy);

        Assert.IsFalse(
            state.IsCompleted);

        Assert.IsFalse(
            state.IsSuccessful);

        Assert.IsFalse(
            state.IsCancelled);

        Assert.IsFalse(
            state.HasError);

        Assert.AreEqual(
            string.Empty,
            state.Message);
    }

    [TestMethod]
    public void Running_IsBusyAndNormalizesMessage()
    {
        AsyncOperationState state =
            AsyncOperationState.Running(
                "  Searching...  ");

        Assert.AreEqual(
            AsyncOperationStatus.Running,
            state.Status);

        Assert.IsTrue(
            state.IsBusy);

        Assert.IsFalse(
            state.IsCompleted);

        Assert.AreEqual(
            "Searching...",
            state.Message);
    }

    [TestMethod]
    public void Succeeded_IsCompletedAndSuccessful()
    {
        AsyncOperationState state =
            AsyncOperationState.Succeeded(
                "Done.");

        Assert.AreEqual(
            AsyncOperationStatus.Succeeded,
            state.Status);

        Assert.IsFalse(
            state.IsBusy);

        Assert.IsTrue(
            state.IsCompleted);

        Assert.IsTrue(
            state.IsSuccessful);

        Assert.IsFalse(
            state.HasError);

        Assert.AreEqual(
            "Done.",
            state.Message);
    }

    [TestMethod]
    public void Cancelled_IsCompletedAndCancelled()
    {
        AsyncOperationState state =
            AsyncOperationState.Cancelled(
                "Cancelled.");

        Assert.AreEqual(
            AsyncOperationStatus.Cancelled,
            state.Status);

        Assert.IsFalse(
            state.IsBusy);

        Assert.IsTrue(
            state.IsCompleted);

        Assert.IsTrue(
            state.IsCancelled);

        Assert.IsFalse(
            state.HasError);
    }

    [TestMethod]
    public void Failed_IsCompletedAndHasError()
    {
        AsyncOperationState state =
            AsyncOperationState.Failed(
                "Failed.");

        Assert.AreEqual(
            AsyncOperationStatus.Failed,
            state.Status);

        Assert.IsFalse(
            state.IsBusy);

        Assert.IsTrue(
            state.IsCompleted);

        Assert.IsTrue(
            state.HasError);

        Assert.IsFalse(
            state.IsSuccessful);

        Assert.AreEqual(
            "Failed.",
            state.Message);
    }

    [TestMethod]
    public void FactoryMethods_ConvertWhitespaceMessageToEmpty()
    {
        AsyncOperationState running =
            AsyncOperationState.Running(
                "   ");

        AsyncOperationState succeeded =
            AsyncOperationState.Succeeded(
                null);

        Assert.AreEqual(
            string.Empty,
            running.Message);

        Assert.AreEqual(
            string.Empty,
            succeeded.Message);
    }
}
