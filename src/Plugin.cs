using System;
using System.Linq;
using MelonLoader;

[assembly: MelonInfo(typeof(Patice.Plugin), "Patice", "0.2.4", "Yeuoly")]
[assembly: MelonGame(null, "Sinmai")]
[assembly: HarmonyDontPatchAll]

namespace Patice;

public sealed class Plugin : MelonMod
{
    public override void OnInitializeMelon()
    {
        HarmonyInstance.PatchAll(typeof(PracticeMode));
        HarmonyInstance.PatchAll(typeof(Playback));
        HarmonyInstance.PatchAll(typeof(ScoreGuard));
        var count = HarmonyInstance.GetPatchedMethods().Count();
        if (count == 0) throw new InvalidOperationException("Patice did not register any game hooks.");
        MelonLogger.Msg($"Patice loaded: {count} game hooks registered.");
    }
}
