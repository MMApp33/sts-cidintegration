namespace STS.CID.Easy;

public sealed record IncomingCall(string PhoneNumber, DateTime ReceivedAt, string? DeviceSerial, string? InputPort);

/// <summary>
/// Holds the latest call not yet picked up by the website, plus a short history for the UI.
/// Matches the contract of the router connector: /api/latest-call returns a call once, then clears it.
/// </summary>
public sealed class CallStore
{
    private const int HistorySize = 50;

    private readonly object _sync = new();
    private readonly LinkedList<IncomingCall> _history = new();
    private IncomingCall? _pending;

    public event EventHandler<IncomingCall>? CallReceived;

    public void Add(IncomingCall call)
    {
        lock (_sync)
        {
            _pending = call;
            _history.AddFirst(call);
            while (_history.Count > HistorySize)
            {
                _history.RemoveLast();
            }
        }

        CallReceived?.Invoke(this, call);
    }

    public IncomingCall? TakePending()
    {
        lock (_sync)
        {
            var call = _pending;
            _pending = null;
            return call;
        }
    }

    public IReadOnlyList<IncomingCall> History
    {
        get
        {
            lock (_sync)
            {
                return _history.ToList();
            }
        }
    }
}
