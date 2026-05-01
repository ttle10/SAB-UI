
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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
        private static readonly JsonSerializerOptions JsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
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
                notifications = body.ValueKind == JsonValueKind.Array
                           ? JsonSerializer.Deserialize<List<ProfileConnectionNotificationModel>>(body, JsonOptions)
                               ?? new List<ProfileConnectionNotificationModel>()
                           : new List<ProfileConnectionNotificationModel>
                           {
                JsonSerializer.Deserialize<ProfileConnectionNotificationModel>(body, JsonOptions)
                ?? throw new JsonException("Invalid notification object.")
                           };

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
            
            var sabDataNotification = _cache.Update(notifications);

            await _hubContext.Clients.All
                .SendAsync("ProfileUpdated", sabDataNotification);

            _logger.LogInformation("Broadcasted {Count} notification(s).", notifications.Count);

            return Ok();
        }
    }
}
