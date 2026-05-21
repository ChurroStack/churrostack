using Yarp.Telemetry.Consumption;

namespace ChurrOS.Api.Services.Share
{
    public sealed class WebSocketsTelemetryConsumer : IWebSocketsTelemetryConsumer
    {
        private readonly ILogger<WebSocketsTelemetryConsumer> _logger;

        public WebSocketsTelemetryConsumer(ILogger<WebSocketsTelemetryConsumer> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }

        public void OnWebSocketClosed(DateTime timestamp, DateTime establishedTime, WebSocketCloseReason closeReason, long messagesRead, long messagesWritten)
        {
            _logger.LogInformation($"WebSocket connection closed ({closeReason}) after reading {messagesRead} and writing {messagesWritten} messages over {(timestamp - establishedTime).TotalSeconds:N2} seconds.");
        }
    }
}
