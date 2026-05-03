
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using SystemeAideBasculement.Hubs;
using SystemeAideBasculement.Models;
using SystemeAideBasculement.Services;

namespace SystemeAideBasculement.Controllers
{

    [ApiController]
    [Route("api/[controller]")]

    public class NotificationsController : ControllerBase
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters =
                {
                    new JsonStringEnumConverter()
                }
            };

        private readonly IHubContext<NotificationHub> _hubContext;

        private readonly ILogger<NotificationsController> _logger;

        private readonly SabStateCache _cache;

        public NotificationsController(IHubContext<NotificationHub> hubContext,
                                      ILogger<NotificationsController> logger,
                                      SabStateCache cache)
        {
            _hubContext = hubContext;
            _logger = logger;
            _cache = cache;
        }

        [HttpPost]
        public IActionResult ReceiveProfileConnectionNotification(
            [FromBody] JsonElement body)
        {

            var senderId =
                Request.Headers.TryGetValue("X-Sender-Id", out var values)
                    ? values.First()
                    : "UNKNOWN";

            _logger.LogDebug(
                "Received profile notification from sender {SenderId}", senderId);

            if (body.ValueKind != JsonValueKind.Array &&
                body.ValueKind != JsonValueKind.Object)
            {
                _logger.LogWarning("Invalid JSON payload received.");
                return BadRequest("Invalid JSON payload.");
            }

            if (!_cache.IsReady)
            {
                _logger.LogWarning("Cache not ready. Rejecting notification.");
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    "Cache not ready.");
            }

            List<ProfileConnectionNotificationModel> notifications;

            try
            {
                if (body.ValueKind == JsonValueKind.Array)
                {
                    notifications =
                        JsonSerializer.Deserialize<List<ProfileConnectionNotificationModel>>(
                            body,
                            JsonOptions)
                        ?? new List<ProfileConnectionNotificationModel>();
                }
                else
                {
                    var single =
                        JsonSerializer.Deserialize<ProfileConnectionNotificationModel>(
                            body,
                            JsonOptions)
                        ?? throw new JsonException("Invalid notification object.");

                    notifications = new List<ProfileConnectionNotificationModel>
                                    {
                                        single
                                    };
                }

                _logger.LogDebug("Deserialized {Count} notification(s).", notifications.Count);
            }
            catch (JsonException ex)
            {
                _logger.LogError( ex,
                                  "[ReceiveProfileConnectionNotification] Failed to deserialize payload");

                return BadRequest("Invalid notification format.");
            }
            if (notifications == null || notifications.Count == 0)
            {
                _logger.LogWarning("Notification list is empty.");
                return BadRequest("Notification list is empty.");
            }
            
            _cache.EnqueueProfileNotification(notifications, senderId ?? "Unknown");

            // 7. Accept immediately (processing happens later)
            return Accepted();
        }
    }

    internal record IncomingNotification(
        ProfileConnectionNotificationModel Notification,
        string SenderId,
        DateTime ReceivedAt
    );
}
