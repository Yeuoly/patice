using MelonLoader;
using Patice.Segments;
using Patice.Game;

[assembly: MelonInfo(typeof(Patice.Plugin), "Patice", "0.2.40", "Yeuoly")]
[assembly: MelonGame(null, "Sinmai")]
[assembly: HarmonyDontPatchAll]

namespace Patice;

public sealed class Plugin : MelonMod
{
    public static SegmentManagement SegmentManagement { get; } = new();
    private static Plugin instance;
    private static bool gameplayHooksInstalled;

    public override void OnInitializeMelon()
    {
        instance = this;
        SegmentManagement.RegisterStore(new BuiltinSegments());
        HarmonyInstance.PatchAll(typeof(GameHooks.Lifecycle));
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
        HarmonyInstance.PatchAll(typeof(GameHooks));
        HarmonyInstance.PatchAll(typeof(PlaybackHooks));
        HarmonyInstance.PatchAll(typeof(ScoreGuard));
    }
}
