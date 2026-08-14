using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Server;

public static class PvpWebSocketStream
{
    private static readonly TimeSpan HeartbeatInterval =
        TimeSpan.FromSeconds(15);
    private static readonly JsonSerializerOptions JsonOptions =
        CreateJsonOptions();

    public static async Task RunAsync(
        WebSocket socket,
        AuthenticatedPlayer player,
        PvpMatchRuntime runtime,
        CancellationToken cancellationToken)
    {
        long observedVersion = -1;
        try
        {
            while (!cancellationToken.IsCancellationRequested &&
                   socket.State == WebSocketState.Open)
            {
                PvpStreamMessageResponse message =
                    runtime.GetStreamState(player);
                byte[] payload = JsonSerializer.SerializeToUtf8Bytes(
                    message,
                    JsonOptions);
                await socket.SendAsync(
                    payload,
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken);
                observedVersion = message.Version;
                await runtime.WaitForStateChangeAsync(
                    observedVersion,
                    HeartbeatInterval,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
        }
        catch (WebSocketException)
        {
            // The peer disappeared. A later reconnect receives a full snapshot.
        }
        finally
        {
            if (socket.State is WebSocketState.Open or
                WebSocketState.CloseReceived)
            {
                try
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "스트림 종료",
                        CancellationToken.None);
                }
                catch (WebSocketException)
                {
                }
            }
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
