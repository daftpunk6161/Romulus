using Romulus.Tests.TestFixtures;
using Romulus.UI.Wpf.ViewModels;
using Xunit;

namespace Romulus.Tests;

/// <summary>
/// Phase-2 Safety (Pragmatic): ConvertOnly muss vor dem Start eine Konsequenz-Bestaetigung
/// einholen (IDialogService.Confirm), da die Konvertierungs-Pipeline destruktive Folge-
/// operationen ausloesen kann (Source-Cleanup nach Verifizierung). Lehnt der Nutzer ab,
/// darf weder ConvertOnly noch DryRun=false gesetzt werden, und es darf kein Run starten.
/// </summary>
public class ConvertOnlyConfirmTests
{
    [Fact]
    public void ConvertOnly_AbortsWhenUserDeclinesConfirmation()
    {
        var dialog = new StubDialogService { ConfirmResult = false };
        var vm = new MainViewModel(new StubThemeService(), dialog);
        vm.Roots.Add(@"C:\TestRoot");

        // Vorbedingung: ConvertOnly ist initial false, DryRun spiegelt Defaults wider.
        var dryRunBefore = vm.DryRun;

        Assert.True(vm.ConvertOnlyCommand.CanExecute(null),
            "ConvertOnlyCommand muss bei vorhandenem Root und Idle-State ausfuehrbar sein");

        vm.ConvertOnlyCommand.Execute(null);

        Assert.False(vm.ConvertOnly,
            "Bei abgelehnter Bestaetigung darf ConvertOnly nicht aktiviert werden");
        Assert.Equal(dryRunBefore, vm.DryRun);
    }

    [Fact]
    public void ConvertOnly_ProceedsWhenUserConfirms()
    {
        var dialog = new StubDialogService { ConfirmResult = true };
        var vm = new MainViewModel(new StubThemeService(), dialog);
        vm.Roots.Add(@"C:\TestRoot");

        vm.ConvertOnlyCommand.Execute(null);

        Assert.True(vm.ConvertOnly,
            "Nach bestaetigter Konsequenz muss ConvertOnly aktiviert sein");
        Assert.False(vm.DryRun,
            "Nach bestaetigter Konsequenz muss DryRun deaktiviert sein, damit der reale Lauf startet");
    }
}
