using HarmonyLib;
using System.IO;

public static class RelicStarterMod
{
    public static void ModLoaded()
    {
        File.AppendAllText("sts2_mod_log.txt", $"\n[{DateTime.Now}] - RelicStarter loaded");

        var harmony = new Harmony("jzhou.relicstarter");
        harmony.PatchAll();
    }
}