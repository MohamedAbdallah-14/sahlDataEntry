using SahelBundleKeyboard.Core.Automation;

namespace SahelBundleKeyboard.Core.Tests.Automation;

public class AsyncGateTests
{
    [Fact]
    public async Task OpenGate_DoesNotBlock()
    {
        var gate = new AsyncGate();
        await gate.WaitAsync(CancellationToken.None);
        Assert.False(gate.IsClosed);
    }

    [Fact]
    public async Task ClosedGate_BlocksUntilOpened()
    {
        var gate = new AsyncGate();
        gate.Close();

        var waitTask = gate.WaitAsync(CancellationToken.None);
        Assert.False(waitTask.IsCompleted);

        gate.Open();
        await waitTask;
        Assert.False(gate.IsClosed);
    }

    [Fact]
    public async Task ClosedGate_CancelsViaToken()
    {
        var gate = new AsyncGate();
        gate.Close();
        using var cts = new CancellationTokenSource(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => gate.WaitAsync(cts.Token));
    }
}
