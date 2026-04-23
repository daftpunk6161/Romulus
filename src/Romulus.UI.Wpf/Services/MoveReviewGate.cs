using Romulus.Contracts.Models;
using Romulus.Contracts.Ports;

namespace Romulus.UI.Wpf.Services;

/// <summary>
/// M6 (UX-Redesign Phase 2) — Review-Gate vor destruktivem Move-Apply.
///
/// Wenn das letzte DryRun-Ergebnis Items mit Status Blocked oder Review enthaelt,
/// wird vor dem realen Move eine zusaetzliche Bestaetigung eingefordert:
///   - Blocked &gt; 0 -&gt; harter DangerConfirm mit getipptem Bestaetigungs-Token
///   - sonst Review &gt; 0 -&gt; einfacher Confirm-Dialog
///
/// Reine Funktion ohne Felder oder I/O ausser dem injizierten Dialog/Logger,
/// damit GUI / CLI / API spaeter dieselbe Logik teilen koennen, ohne
/// Schattenpfade zu erzeugen (Single Source of Truth fuer das Review-Gate).
/// </summary>
internal static class MoveReviewGate
{
    /// <summary>
    /// Prueft das DryRun-Ergebnis und holt bei Bedarf eine zusaetzliche
    /// Bestaetigung beim Nutzer ein.
    /// </summary>
    /// <returns>
    /// <c>true</c> wenn der Move fortgesetzt werden darf,
    /// <c>false</c> wenn der Nutzer abbricht (oder kein Dialog liefern konnte).
    /// </returns>
    public static bool EvaluateBeforeMove(
        RunResult? lastRunResult,
        IDialogService dialog,
        Func<string, string> loc,
        Action<string, string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(loc);

        if (lastRunResult is null)
        {
            return true;
        }

        var blocked = lastRunResult.ConvertBlockedCount;
        var review = lastRunResult.ConvertReviewCount;

        if (blocked > 0)
        {
            var title = loc("Dialog.MoveReviewGate.BlockedTitle");
            var message = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                loc("Dialog.MoveReviewGate.BlockedMessage"),
                blocked);
            var confirmText = loc("Dialog.MoveReviewGate.BlockedConfirmText");
            var buttonLabel = loc("Dialog.MoveReviewGate.BlockedButton");

            var confirmed = dialog.DangerConfirm(title, message, confirmText, buttonLabel);
            if (!confirmed)
            {
                log?.Invoke(loc("Log.MoveReviewGate.Cancelled"), "INFO");
                return false;
            }

            return true;
        }

        if (review > 0)
        {
            var title = loc("Dialog.MoveReviewGate.ReviewTitle");
            var message = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                loc("Dialog.MoveReviewGate.ReviewMessage"),
                review);

            var confirmed = dialog.Confirm(message, title);
            if (!confirmed)
            {
                log?.Invoke(loc("Log.MoveReviewGate.Cancelled"), "INFO");
                return false;
            }
        }

        return true;
    }
}
