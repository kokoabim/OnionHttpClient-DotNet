namespace Kokoabim.OnionHttpClient;

public class AsyncManualResetEvent : IDisposable
{
    public bool IsSet => _tcs.Task.IsCompleted;

    private volatile TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public AsyncManualResetEvent(bool initialState = false)
    {
        if (initialState) Set();
    }

    public void Reset()
    {
        while (true)
        {
            var tcs = _tcs;
            if (!tcs.Task.IsCompleted
                || Interlocked.CompareExchange(
                    ref _tcs,
                    new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
                    tcs) == tcs
                )
                return;
        }
    }

    public bool Set() => _tcs.TrySetResult(true);

    public Task WaitAsync(CancellationToken cancellationToken = default) =>
        cancellationToken == default ? _tcs.Task : Task.WhenAny(_tcs.Task, Task.Delay(Timeout.Infinite, cancellationToken));

    ~AsyncManualResetEvent()
    {
        Dispose(disposing: false);
    }

    #region IDisposable

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_tcs != null)
            {
                if (!IsSet) _tcs.TrySetCanceled();
                _tcs = null!;
            }
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion 
}