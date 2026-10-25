using System;
using System.Linq;
using MelonLoader;

[assembly: MelonInfo(typeof(Patice.Plugin), "Patice", "0.2.10", "Yeuoly")]
[assembly: MelonGame(null, "Sinmai")]
[assembly: HarmonyDontPatchAll]

namespace Patice;

public sealed class Plugin : MelonMod
{
    private static Plugin instance;
    private static bool gameplayHooksInstalled;

    public override void OnInitializeMelon()
    {
        instance = this;
        HarmonyInstance.PatchAll(typeof(PracticeMode.Lifecycle));
        MelonLogger.Msg("Patice loaded; waiting for gameplay.");
    }

    internal static void InstallGameplayHooks()
    {
        if (gameplayHooksInstalled) return;
        instance.InstallHooks();
        gameplayHooksInstalled = true;
    }

    private void InstallHooks()
    {
        HarmonyInstance.PatchAll(typeof(PracticeMode));
        HarmonyInstance.PatchAll(typeof(Playback));
        HarmonyInstance.PatchAll(typeof(ScoreGuard));
        var count = HarmonyInstance.GetPatchedMethods().Count();
        if (count == 0) throw new InvalidOperationException("Patice did not register any game hooks.");
        MelonLogger.Msg($"Practice ready: {count} game hooks registered.");
    }
}
