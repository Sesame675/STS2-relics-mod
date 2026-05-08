using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

[HarmonyPatch(typeof(RunManager), "EnterRoom")]
public static class RelicStarterPatch
{
    // Prevents relics from being added multiple times in the same run.
    private static bool addedThisRun = false;

    // Tracks the current RunState --> reset addedThisRun when a new run starts.
    private static RunState? lastRunState = null;

    [HarmonyPostfix]
    public static async void Postfix(AbstractRoom room)
    {
        RunState? runState = RunManager.Instance.DebugOnlyGetState();
        if (runState == null) return;

        // If the RunState object changed, the player started a new run.
        if (!ReferenceEquals(lastRunState, runState))
        {
            lastRunState = runState;
            addedThisRun = false;
            File.AppendAllText("sts2_mod_log.txt", $"\n[{DateTime.Now}] New run detected, reset flag");
        }

        if (addedThisRun) return;

        var player = runState.Players.FirstOrDefault();
        if (player == null) return;

        foreach (var relicName in LoadConfig())
        {
            await AddRelic(player, relicName);
        }

        addedThisRun = true;
        File.AppendAllText("sts2_mod_log.txt", $"\n[{DateTime.Now}] - Finished adding all relics");
    }

    private static List<string> LoadConfig()
    {
        string path = "mods/RelicStarter/config.txt";

        if (!File.Exists(path))
        {
            Directory.CreateDirectory("mods/RelicStarter");
            // Default config created on first run.
            File.WriteAllText(path, "OLD_COIN\nVAJRA\nTOXIC_EGG");
        }

        // Supports both newline-separated and comma-separated relic IDs.
        return File.ReadAllText(path)
            .Split(new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().ToUpper())
            .Where(x => x.Length > 0)
            .ToList();
    }

// 这个接口在public beta版本改了，得用RelicCmd了
/*
    private static async Task AddRelic(Player player, string relicName)
    {
        // Relic IDs use internal names, e.g. OLD_COIN, VAJRA, TOXIC_EGG.
        var relicModel = ModelDb.AllRelics
            .FirstOrDefault(r => r.Id.Entry == relicName);

        if (relicModel == null)
        {
            File.AppendAllText("sts2_mod_log.txt", $"\n[{DateTime.Now}] - Relic not found: {relicName}");
            return;
        }

        // Avoid duplicate non-stackable relics.
        if (player.GetRelicById(relicModel.Id) != null)
        {
            File.AppendAllText("sts2_mod_log.txt", $"\n[{DateTime.Now}] - Already has relic: {relicName}");
            return;
        }

        // Use the game's reward flow instead of manually adding to player.Relics.
        // it's already built-in the game with full logic and UI updates
        var relicReward = new RelicReward(relicModel.ToMutable(), player);
        await relicReward.OnSelectWrapper();

        await SaveManager.Instance.SaveRun(null);

        File.AppendAllText("sts2_mod_log.txt", $"\n[{DateTime.Now}] - Added relic: {relicName}");
    }
*/
    private static async Task AddRelic(Player player, string relicName)
    {
        // Search for the relic by its internal ID name.
        // Example: OLD_COIN, VAJRA, TOXIC_EGG
        var relicModel = ModelDb.AllRelics
            .FirstOrDefault(r => r.Id.Entry == relicName);

        // Invalid relic ID in config.txt
        if (relicModel == null)
        {
            File.AppendAllText("sts2_mod_log.txt", $"\n[{DateTime.Now}] - Relic not found: {relicName}");
            return;
        }

        // Prevent duplicate relics for non-stackable relics.
        if (player.GetRelicById(relicModel.Id) != null)
        {
            File.AppendAllText("sts2_mod_log.txt", $"\n[{DateTime.Now}] - Already has relic: {relicName}");
            return;
        }

        // Create a mutable runtime instance from the canonical relic model.
        RelicModel relic = relicModel.ToMutable();

        // Official game command for obtaining relics.
        await RelicCmd.Obtain(relic, player);

        // Save the run after adding the relic.
        await SaveManager.Instance.SaveRun(null);

        File.AppendAllText("sts2_mod_log.txt", $"\n[{DateTime.Now}] - Added relic: {relicName}");
    }

}
