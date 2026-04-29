
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using SystemeAideBasculement.Hubs;
using SystemeAideBasculement.Models;

namespace SystemeAideBasculement.Controllers
{

    [ApiController]
    [Route("api/[controller]")]

    public class NotificationsController : ControllerBase
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

        private readonly IHubContext<NotificationHub> _hubContext;

        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(IHubContext<NotificationHub> hubContext,
                                      ILogger<NotificationsController> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> ReceiveProfileConnectionNotification(
            [FromBody] JsonElement body)
        {
            if (body.ValueKind != JsonValueKind.Array &&
                body.ValueKind != JsonValueKind.Object)
            {
                _logger.LogWarning("Invalid JSON payload received.");
                return BadRequest("Invalid JSON payload.");
            }   

            List<ProfileConnectionNotificationModel> notifications;

            try
            {
                //notifications = body.ValueKind == JsonValueKind.Array
                //    ? JsonSerializer.Deserialize<List<ProfileConnectionNotificationModel>>(body, JsonOptions)
                //    : new List<ProfileConnectionNotificationModel>
                //    {
                //JsonSerializer.Deserialize<ProfileConnectionNotificationModel>(body, JsonOptions)
                //    };

                var single = JsonSerializer.Deserialize<ProfileConnectionNotificationModel>(body, JsonOptions);
                if (single == null)
                {
                    _logger.LogError("Failed to deserialize notification payload.");
                    return BadRequest("Invalid notification.");
                }
                    
                notifications = new List<ProfileConnectionNotificationModel> { single };
                _logger.LogDebug("Deserialized {Count} notification(s).", notifications?.Count ?? 0);
            }
            catch (JsonException)
            {
                _logger.LogError("Failed to deserialize notification payload.");
                return BadRequest("Invalid notification format.");
            }

            if (notifications == null || notifications.Count == 0)
            {
                _logger.LogWarning("Notification list is empty.");
                return BadRequest("Notification list is empty.");
            }

            await _hubContext.Clients.All
                .SendAsync("ProfileUpdated", notifications);

            _logger.LogInformation("Broadcasted {Count} notification(s).", notifications.Count);

            return Ok();
        }
    }
}
