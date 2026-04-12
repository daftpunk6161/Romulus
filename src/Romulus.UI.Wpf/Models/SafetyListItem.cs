namespace Romulus.UI.Wpf.Models;

public sealed record SafetyListItem(
    string FileName,
    string ConsoleKey,
    string MatchLevel,
    string Reason);
