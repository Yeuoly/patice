using System.Reflection;
using MelonLoader;

[assembly: MelonInfo(typeof(Patice.Plugin), "Patice", "0.2.0", "Yeuoly")]
[assembly: MelonGame(null, "Sinmai")]
[assembly: HarmonyDontPatchAll]

namespace Patice;

public sealed class Plugin : MelonMod
{
    public override void OnInitializeMelon()
    {
        HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        MelonLogger.Msg("Patice loaded.");
    }
}
