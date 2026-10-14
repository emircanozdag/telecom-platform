using TelecomPlatform.Contracts;

namespace Notification.Api.Domain;

public static class NotificationTemplates
{
    public static IReadOnlyList<NotificationCommand> For(QuotaThresholdReached e)
    {
        var usedGb = e.UsedMegabytes / 1024m;
        var quotaGb = e.QuotaMegabytes / 1024m;

        var sms = e.ThresholdPercent >= 100
            ? $"Your monthly data quota is used up ({usedGb:0.#}/{quotaGb:0.#} GB). Extra usage is billed per MB."
            : $"You have used {e.ThresholdPercent}% of your monthly data ({usedGb:0.#}/{quotaGb:0.#} GB).";

        return
        [
            new NotificationCommand(e.EventId, e.SubscriberId, NotificationChannel.Sms, e.Msisdn, sms),
            new NotificationCommand(e.EventId, e.SubscriberId, NotificationChannel.Email, e.Email,
                $"Data usage alert: {e.ThresholdPercent}% of your quota\n\n{sms}"),
        ];
    }
}
