using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using SystemeAideBasculement.Models;
using SystemeAideBasculement.Services;

namespace SystemeAideBasculement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly ILogger<NotificationsController> _logger;
        private readonly INotificationQueue _notificationQueue;

        public NotificationsController(
            ILogger<NotificationsController> logger,
            INotificationQueue notificationQueue)
        {
            _logger = logger;
            _notificationQueue = notificationQueue;
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveProfileConnectionNotification(
            [FromBody] JsonElement body)
        {
            var senderId =
                Request.Headers.TryGetValue("X-Sender-Id", out var values)
                    ? values.FirstOrDefault() ?? "UNKNOWN"
                    : "UNKNOWN";

            _logger.LogTrace(
                "[SabUI:NotificationsController:ReceiveProfileConnectionNotification]: Received profile notification from sender {SenderId}",
                senderId);

            LogNotificationBody(body);

            if (body.ValueKind != JsonValueKind.Array &&
                body.ValueKind != JsonValueKind.Object)
            {
                _logger.LogWarning(
                    "[SabUI:NotificationsController:ReceiveProfileConnectionNotification]: Invalid JSON payload received.");
                return BadRequest("Invalid JSON payload.");
            }

            List<ProfileConnectionNotificationModel> notifications;

            try
            {
                var rawJson = body.GetRawText();

                if (string.IsNullOrWhiteSpace(rawJson))
                {
                    _logger.LogError(
                        "[SabUI:NotificationsController:ReceiveProfileConnectionNotification]: Empty JSON payload received.");
                    return BadRequest("Empty JSON payload.");
                }

                if (body.ValueKind == JsonValueKind.Array)
                {
                    notifications = JsonHelper.DeserializeList<ProfileConnectionNotificationModel>(rawJson);
                }
                else
                {
                    var single = JsonHelper.Deserialize<ProfileConnectionNotificationModel>(rawJson);
                    notifications = new List<ProfileConnectionNotificationModel> { single };
                }

                _logger.LogTrace(
                    "[SabUI:NotificationsController:ReceiveProfileConnectionNotification]: Deserialized {Count} notification(s).",
                    notifications.Count);
            }
            catch (JsonException ex)
            {
                _logger.LogError(
                    ex,
                    "[SabUI:NotificationsController:ReceiveProfileConnectionNotification]: Failed to deserialize payload.");

                return BadRequest("Invalid notification format.");
            }

            if (notifications == null || notifications.Count == 0)
            {
                _logger.LogWarning(
                    "[SabUI:NotificationsController:ReceiveProfileConnectionNotification]: Notification list is empty.");
                return BadRequest("Notification list is empty.");
            }

            var receivedAt = DateTime.UtcNow;

            foreach (var notification in notifications)
            {
                var incoming = new IncomingNotification(
                    notification,
                    senderId,
                    receivedAt);

                await _notificationQueue.EnqueueAsync(
                    incoming,
                    HttpContext.RequestAborted);
            }
            
            if (_notificationQueue.Count > 0)
            {
                _logger.LogTrace(
                    "[SabUI:NotificationsController:ReceiveProfileConnectionNotification]: After enqueueing, {_notificationQueue.Count} notification(s) in the queue.",
                    _notificationQueue.Count);
            }
            else
            {
                _logger.LogWarning(
                    "[SabUI:NotificationsController:ReceiveProfileConnectionNotification]: No notifications were enqueued.");
            }

            return Accepted();
        }

        private void LogNotificationBody(JsonElement body)
        {
            try
            {
                var prettyJson = JsonSerializer.Serialize(
                    body,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                _logger.LogInformation(
                    "[SabUI:NotificationsController:ReceiveProfileConnectionNotification]: Received notification payload:\n{Payload}",
                    prettyJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[SabUI:NotificationsController:ReceiveProfileConnectionNotification]: Failed to log notification payload.");
            }
        }
    }
}