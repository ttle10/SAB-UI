using SystemeAideBasculement.Models;

namespace SystemeAideBasculement.Services
{
    public record IncomingNotification(
        ProfileConnectionNotificationModel Notification,
        string SenderId,
        DateTime ReceivedAt
    );
}
