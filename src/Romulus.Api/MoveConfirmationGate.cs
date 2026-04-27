using Romulus.Contracts;

namespace Romulus.Api;

/// <summary>
/// F-01 (CLI/API parity audit, Apr 2026): typed-confirmation-token gate for the API
/// POST /run endpoint when mode == Move. Mirrors the GUI's DangerConfirm dialog
/// (Dialog.Move.ConfirmText = "MOVE") so destructive runs require an explicit
/// caller intent across all entry points.
///
/// Contract:
///   - DryRun (or null/empty mode) does not require any confirmation header.
///   - Move requires the X-Confirm-Token request header set exactly to "MOVE"
///     (case-sensitive, ordinal compare). Anything else (missing, lower-case,
///     "yes", "true", ...) is rejected with HTTP 400 / RUN-MOVE-CONFIRMATION-REQUIRED.
/// </summary>
internal static class MoveConfirmationGate
{
    /// <summary>Required header name carrying the confirmation token.</summary>
    public const string HeaderName = "X-Confirm-Token";

    /// <summary>Exact, case-sensitive token value the caller must send.</summary>
    public const string ConfirmationToken = "MOVE";

    /// <summary>True when the given run mode requires the confirmation token.</summary>
    public static bool RequiresConfirmation(string? mode)
    {
        return !string.IsNullOrEmpty(mode)
            && string.Equals(mode, RunConstants.ModeMove, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True when the given (mode, token) pair is permitted to proceed.
    /// DryRun always passes; Move requires token == "MOVE" (ordinal).
    /// </summary>
    public static bool IsValidConfirmationToken(string? mode, string? token)
    {
        if (!RequiresConfirmation(mode))
            return true;

        return string.Equals(token, ConfirmationToken, StringComparison.Ordinal);
    }
}
