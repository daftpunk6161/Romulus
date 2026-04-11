namespace Romulus.Infrastructure.Watch;

/// <summary>
/// Shared cron matcher used by GUI, CLI, and API schedule automation.
/// Supports five-field expressions: minute hour day month day-of-week.
/// </summary>
public static class CronScheduleEvaluator
{
    public static bool TryValidateCronExpression(string cronExpression, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            errorMessage = "cron must not be empty.";
            return false;
        }

        var fields = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5)
        {
            errorMessage = "cron must contain exactly five fields.";
            return false;
        }

        if (!TryValidateField(fields[0], minValue: 0, maxValue: 59, "minute", out errorMessage))
            return false;

        if (!TryValidateField(fields[1], minValue: 0, maxValue: 23, "hour", out errorMessage))
            return false;

        if (!TryValidateField(fields[2], minValue: 1, maxValue: 31, "day-of-month", out errorMessage))
            return false;

        if (!TryValidateField(fields[3], minValue: 1, maxValue: 12, "month", out errorMessage))
            return false;

        if (!TryValidateField(fields[4], minValue: 0, maxValue: 6, "day-of-week", out errorMessage))
            return false;

        return true;
    }

    public static bool TestCronMatch(string cronExpression, DateTime dateTime)
    {
        if (!TryValidateCronExpression(cronExpression, out _))
            return false;

        var fields = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return CronFieldMatch(fields[0], dateTime.Minute)
               && CronFieldMatch(fields[1], dateTime.Hour)
               && CronFieldMatch(fields[2], dateTime.Day)
               && CronFieldMatch(fields[3], dateTime.Month)
               && CronFieldMatch(fields[4], (int)dateTime.DayOfWeek);
    }

    public static bool CronFieldMatch(string field, int value)
    {
        if (field == "*")
            return true;

        foreach (var part in field.Split(','))
        {
            if (part.Contains('/'))
            {
                var segments = part.Split('/');
                if (segments.Length == 2 && int.TryParse(segments[1], out var step) && step > 0)
                {
                    if (segments[0].Contains('-'))
                    {
                        var range = segments[0].Split('-');
                        if (range.Length == 2
                            && int.TryParse(range[0], out var rangeStart)
                            && int.TryParse(range[1], out var rangeEnd)
                            && value >= rangeStart
                            && value <= rangeEnd
                            && (value - rangeStart) % step == 0)
                        {
                            return true;
                        }
                    }
                    else if (segments[0] == "*")
                    {
                        const int effectiveStart = 0;
                        if (value >= effectiveStart && (value - effectiveStart) % step == 0)
                            return true;
                    }
                    else if (int.TryParse(segments[0], out var start))
                    {
                        var effectiveStart = start;
                        if (value >= effectiveStart && (value - effectiveStart) % step == 0)
                            return true;
                    }
                }

                continue;
            }

            if (part.Contains('-'))
            {
                var range = part.Split('-');
                if (range.Length == 2
                    && int.TryParse(range[0], out var start)
                    && int.TryParse(range[1], out var end)
                    && value >= start
                    && value <= end)
                {
                    return true;
                }

                continue;
            }

            if (int.TryParse(part, out var exact) && exact == value)
                return true;
        }

        return false;
    }

    private static bool TryValidateField(string field, int minValue, int maxValue, string fieldName, out string? errorMessage)
    {
        errorMessage = null;

        var parts = field.Split(',', StringSplitOptions.None);
        foreach (var rawPart in parts)
        {
            var part = rawPart.Trim();
            if (part.Length == 0)
            {
                errorMessage = $"cron {fieldName} field contains an empty segment.";
                return false;
            }

            if (!TryValidateSegment(part, minValue, maxValue, fieldName, out errorMessage))
                return false;
        }

        return true;
    }

    private static bool TryValidateSegment(string segment, int minValue, int maxValue, string fieldName, out string? errorMessage)
    {
        errorMessage = null;

        string rangeToken = segment;
        if (segment.Contains('/'))
        {
            var stepParts = segment.Split('/', StringSplitOptions.None);
            if (stepParts.Length != 2 || string.IsNullOrWhiteSpace(stepParts[0]))
            {
                errorMessage = $"cron {fieldName} field has invalid step syntax '{segment}'.";
                return false;
            }

            if (!int.TryParse(stepParts[1], out var step) || step <= 0)
            {
                errorMessage = $"cron {fieldName} field must use a positive step value in '{segment}'.";
                return false;
            }

            rangeToken = stepParts[0];
        }

        if (rangeToken == "*")
            return true;

        if (rangeToken.Contains('-'))
        {
            var bounds = rangeToken.Split('-', StringSplitOptions.None);
            if (bounds.Length != 2
                || !TryParseCronNumber(bounds[0], minValue, maxValue, fieldName, out var start, out errorMessage)
                || !TryParseCronNumber(bounds[1], minValue, maxValue, fieldName, out var end, out errorMessage))
            {
                return false;
            }

            if (start > end)
            {
                errorMessage = $"cron {fieldName} field range start must be <= end in '{segment}'.";
                return false;
            }

            return true;
        }

        return TryParseCronNumber(rangeToken, minValue, maxValue, fieldName, out _, out errorMessage);
    }

    private static bool TryParseCronNumber(string token, int minValue, int maxValue, string fieldName, out int value, out string? errorMessage)
    {
        value = 0;
        errorMessage = null;
        var trimmed = token.Trim();
        if (!int.TryParse(trimmed, out value))
        {
            errorMessage = $"cron {fieldName} field contains an invalid number '{trimmed}'.";
            return false;
        }

        if (value < minValue || value > maxValue)
        {
            errorMessage = $"cron {fieldName} field value '{value}' must be between {minValue} and {maxValue}.";
            return false;
        }

        return true;
    }
}
