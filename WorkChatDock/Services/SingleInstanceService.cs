using System.IO.Pipes;
using System.Text;

namespace WorkChatDock.Services;

public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = @"Local\WorkChatDock.Singleton.7F52D90A";
    private const string PipeName = "WorkChatDock.Command.7F52D90A";
    private readonly CancellationTokenSource _cancellation = new();
    private Mutex? _mutex;
    private Task? _serverTask;

    public event Action<string>? CommandReceived;

    public bool TryAcquire()
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (createdNew)
        {
            _serverTask = Task.Run(RunServerAsync);
        }

        return createdNew;
    }

    public static async Task SendCommandAsync(string command)
    {
        try
        {
            await using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out,
                PipeOptions.Asynchronous);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(timeout.Token);
            await using var writer = new StreamWriter(client, new UTF8Encoding(false)) { AutoFlush = true };
            await writer.WriteLineAsync(command);
        }
        catch
        {
            // The original process may still be starting; a second instance simply exits.
        }
    }

    private async Task RunServerAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(_cancellation.Token);
                using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, leaveOpen: true);
                var command = await reader.ReadLineAsync(_cancellation.Token);
                if (!string.IsNullOrWhiteSpace(command))
                {
                    CommandReceived?.Invoke(command);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(250, _cancellation.Token).ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        if (_mutex is not null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
                // The process is already shutting down.
            }

            _mutex.Dispose();
        }

        _cancellation.Dispose();
    }
}
