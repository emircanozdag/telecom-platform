using System.Text.RegularExpressions;

namespace Subscriber.Api.Features;

internal static partial class RequestValidator
{
    // Turkish mobile numbers in E.164 without the plus sign, e.g. 905321234567.
    [GeneratedRegex(@"^905\d{9}$")]
    private static partial Regex MsisdnPattern();

    public static Dictionary<string, string[]> Validate(CreateSubscriberRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Msisdn) || !MsisdnPattern().IsMatch(request.Msisdn))
        {
            errors[nameof(request.Msisdn)] = ["MSISDN must look like 905XXXXXXXXX."];
        }

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@') || request.Email.Length > 254)
        {
            errors[nameof(request.Email)] = ["A valid email address is required."];
        }

        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Length > 200)
        {
            errors[nameof(request.FullName)] = ["Full name is required (max 200 characters)."];
        }

        if (string.IsNullOrWhiteSpace(request.PlanCode))
        {
            errors[nameof(request.PlanCode)] = ["Plan code is required."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(RecordUsageRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        // A single usage record above 100 GB is almost certainly a client bug.
        if (request.Megabytes is <= 0 or > 102_400)
        {
            errors[nameof(request.Megabytes)] = ["Megabytes must be between 1 and 102400."];
        }

        return errors;
    }
}
