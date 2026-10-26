using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using DB;
using Manager;
using Monitor.Game;

namespace Patice.Game;

[HarmonyPatch]
internal static class PlaybackHooks
{
    [HarmonyPatch(typeof(GameCtrl), "UpdateCtrl"), HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> PreserveSkipResult(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        // The debug seek branch discards failure and marks the note used anyway.
        // Preserve the result, as the native track-skip branch already does.
        var result = generator.DeclareLocal(typeof(bool));
        var matcher = new CodeMatcher(instructions).MatchForward(false,
            new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(GameCtrl), "SkipRegistNote")),
            new CodeMatch(OpCodes.Pop),
            new CodeMatch(instruction => instruction.IsLdloc()),
            new CodeMatch(OpCodes.Ldc_I4_1),
            new CodeMatch(instruction => instruction.opcode == OpCodes.Stfld &&
                Equals(instruction.operand, AccessTools.Field(typeof(NoteData), "isUsed"))));
        matcher.ThrowIfInvalid("Native seek note assignment was not found.");
        matcher.Advance(1).Set(OpCodes.Stloc, result);
        matcher.Advance(2).Set(OpCodes.Ldloc, result);
        return matcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(GameManager), "GetNoteSpeed"), HarmonyPrefix]
    private static void PreviewNoteSpeed(ref int __0)
    {
        if (GameHooks.Playback?.Previewing == true) __0 = (int)OptionNotespeedID.Speed_6_75;
    }

    [HarmonyPatch(typeof(GameManager), "GetTouchSpeed"), HarmonyPrefix]
    private static void PreviewTouchSpeed(ref int __0)
    {
        if (GameHooks.Playback?.Previewing == true) __0 = (int)OptionTouchspeedID.Speed6_75;
    }

    [HarmonyPatch(typeof(SoundManager), "PlayExecute"), HarmonyPostfix]
    private static void ApplyQueuedAudioRate()
    {
        GameHooks.Playback?.ApplyQueuedAudioRate();
    }

    [HarmonyPatch(typeof(NotesManager), "UpdateTimer"), HarmonyPostfix]
    private static void MapChartTime(ref float ____curMSec)
    {
        GameHooks.Playback?.MapChartTime(ref ____curMSec);
    }
}
