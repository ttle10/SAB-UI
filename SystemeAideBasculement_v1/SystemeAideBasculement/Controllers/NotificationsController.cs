
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Cryptography;
using System.Text.Json;
using SystemeAideBasculement.Hubs;
using SystemeAideBasculement.Models;
using SystemeAideBasculement.Services;

namespace SystemeAideBasculement.Controllers
{

    [ApiController]
    [Route("api/[controller]")]

    public class NotificationsController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        private readonly ILogger<NotificationsController> _logger;

        private readonly SabStateCache _cache;

        private readonly JsonSchemaProvider _schemaProvider;

        public NotificationsController(IWebHostEnvironment env,
                                      ILogger<NotificationsController> logger,
                                      SabStateCache cache,
                                      JsonSchemaProvider schemaProvider)
        {
            _env = env;
            _logger = logger;
            _cache = cache;
            _schemaProvider = schemaProvider;
        }

        [HttpPost]
        public IActionResult ReceiveProfileConnectionNotification(
            [FromBody] JsonElement body)
        {

            var senderId =
                Request.Headers.TryGetValue("X-Sender-Id", out var values)
                    ? values.First()
                    : "UNKNOWN";

            _logger.LogTrace(
                "[SabUI:NotificationsController:ReceiveProfileConnectionNotification]: Received profile notification from sender {SenderId}", senderId);

            LogNoficationBody(body);

            if (body.ValueKind != JsonValueKind.Array &&
                body.ValueKind != JsonValueKind.Object)
            {
                _logger.LogWarning("[SabUI:NotificationsController:ReceiveProfileConnectionNotification]: Invalid JSON payload received.");
                return BadRequest("Invalid JSON payload.");
            }

            if (!_cache.IsReady)
            {
                _logger.LogWarning("[SabUI:NotificationsController:ReceiveProfileConnectionNotification]: Cache not ready. Rejecting notification.");
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    "Cache not ready.");
            }

            List<ProfileConnectionNotificationModel> notifications;

            try
            {
                var rawJson = body.GetRawText();

                var schema = _schemaProvider.Get("ClientNotification");
                bool isValid = JsonHelper.Validate(rawJson, schema, out var jsonValidationError);
                if (!isValid)
                {
                    _logger.LogError("[SabUI:NotificationsController:ReceiveProfileConnectionNotification] Failed to deserialize payload: {Error}", jsonValidationError);

                    return new ContentResult
                    {
                        Content = jsonValidationError,
                        ContentType = "application/json",
                        StatusCode = StatusCodes.Status400BadRequest
                    };
                }

                if (body.ValueKind == JsonValueKind.Array)
                {
                    notifications = JsonHelper.DeserializeList<ProfileConnectionNotificationModel>(rawJson);
                }
                else
                {
                    var single = JsonHelper.Deserialize<ProfileConnectionNotificationModel>(rawJson);
                    notifications = new List<ProfileConnectionNotificationModel>
                                    {
                                        single
                                    };
                }

                _logger.LogTrace("[SabUI:NotificationsController:ReceiveProfileConnectionNotification]: Deserialized {Count} notification(s).", notifications.Count);
            }
            catch (JsonException ex)
            {
                _logger.LogError( ex,
                                  "[SabUI:NotificationsController:ReceiveProfileConnectionNotification] Failed to deserialize payload");

                return BadRequest("Invalid notification format.");
            }
            if (notifications == null || notifications.Count == 0)
            {
                _logger.LogWarning("[SabUI:NotificationsController:ReceiveProfileConnectionNotification]: Notification list is empty.");
                return BadRequest("Notification list is empty.");
            }
            
            _cache.EnqueueProfileNotification(notifications, senderId ?? "Unknown");

            // 7. Accept immediately (processing happens later)
            return Accepted();
        }

        private void LogNoficationBody(JsonElement body)
        {
            try
            {
                var prettyJson = JsonSerializer.Serialize(
                    body,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                _logger.LogInformation("[SabUI:NotificationsController:ReceiveProfileConnectionNotification]: Received notification payload:\n{Payload}", prettyJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SabUI:NotificationsController:ReceiveProfileConnectionNotification]: Failed to log notification payload.");
            }
        }
    }

    internal record IncomingNotification(
        ProfileConnectionNotificationModel Notification,
        string SenderId,
        DateTime ReceivedAt
    );
}
