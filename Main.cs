using System;
using UnityModManagerNet;
using HarmonyLib;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker;
using System.Linq;
using System.Diagnostics;
using System.IO;
using CodexLib;
using BlueprintCore.Utils;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic;
using System.Security.Permissions;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.Blueprints;

namespace HolyVindicator
{
    public class Main
    {

        public static Harmony harmony;
        public static bool IsInGame => Game.Instance.Player?.Party?.Any() ?? false; // RootUIContext.Instance?.IsInGame ?? false; //

        /// <summary>True if mod is enabled. Doesn't do anything right now.</summary>
        public static bool Enabled { get; set; } = true;
        /// <summary>Path of current mod.</summary>
        public static string ModPath;

        internal static UnityModManager.ModEntry.ModLogger logger;

        [System.Diagnostics.Conditional("DEBUG")]
        internal static void PrintDebug(string msg)
        {
            Main.logger?.Log(msg);
        }

        internal static void Print(string msg)
        {
            Main.logger?.Log(msg);
        }

        internal static void PrintError(string msg)
        {
            Main.logger?.Log("[Exception/Error] " + msg);
        }

        internal static void PrintException(Exception ex)
        {
            Main.logger?.LogException(ex);
        }

        internal static Exception Error(String message)
        {
            Main.PrintError(message);
            return new InvalidOperationException(message);
        }

        /// <summary>Called when the mod is turned to on/off.
        /// With this function you control an operation of the mod and inform users whether it is enabled or not.</summary>
        /// <param name="value">true = mod to be turned on; false = mod to be turned off</param>
        /// <returns>Returns true, if state can be changed.</returns>
        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            Main.Enabled = value;
            return true;
        }

        /// <summary>
        /// Loads on Game Start
        /// </summary>
        /// <param name="modEntry"></param>
        /// <returns></returns>
        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            ModPath = modEntry.Path;
            logger = modEntry.Logger;
            modEntry.OnToggle = OnToggle;
            modEntry.OnUnload = Unload;

            try
            {
                EnsureCodexLib(modEntry.Path);

                harmony = new Harmony(modEntry.Info.Id);
                harmony.PatchAll();
                return true;
            }
            catch (Exception ex)
            {
                Main.PrintException(ex);
                return false;
            }

        }

        private static void EnsureCodexLib(string modPath)
        {
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName.StartsWith("CodexLib, ")))
            {
                return;
            }

            string path = null;
            Version version = null;

            foreach (string cPath in Directory.GetFiles(Directory.GetParent(modPath).FullName, "CodexLib.dll"))
            {
                var cVersion = new Version(FileVersionInfo.GetVersionInfo(cPath).FileVersion);
                if (version == null || cVersion > version)
                {
                    path = cPath;
                    version = cVersion;
                }
            }

            if (path != null)
            {
                Print("Loading CodexLib " + path);
                AppDomain.CurrentDomain.Load(File.ReadAllBytes(path));
            }
        }

        public static bool Unload(UnityModManager.ModEntry modEntry)
        {
            harmony?.UnpatchAll(modEntry.Info.Id);
            return true;
        }


    }

    [HarmonyPatch(typeof(StartGameLoader), "LoadAllJson")]
    static class StartGameLoader_LoadAllJson
    {
        private static bool Run = false;

#pragma warning disable IDE0051 // Remove unused private members
        static void Postfix()
#pragma warning restore IDE0051 // Remove unused private members
        {
            if (Run) return; Run = true;

#if DEBUG
            using var scope = new Scope(modPath: Main.ModPath, logger: Main.logger, harmony: Main.harmony, allowGuidGeneration: true);
#else
            using var scope = new Scope(modPath: Main.ModPath, logger: Main.logger, harmony: Main.harmony, allowGuidGeneration: false);
#endif

            MasterPatch.Run();

            LocalizationTool.LoadLocalizationPacks(new String[2] { Main.ModPath + "l8n\\HolyVindicatorLocalized.json", Main.ModPath + "l8n\\ChannelEnergyEngine.json" });

            LoadSafe(Rebalance.fixChannelEnergyScaling);
            Main.Print("Finished Channel Energy Scaling Fix");

            LoadSafe(ChannelEnergyEngine.init);
            Main.Print("Finished loading Channel Energy Engine");

            LoadSafe(HolyVindicator.Configure);
            Main.Print("Finished loading Holy Vindicator");

        }


        public static bool LoadSafe(Action action)
        {
            string name = action.Method.DeclaringType.Name + "." + action.Method.Name;

            try
            {
                Main.logger?.Log($"Loading {name}");
                action();

                return true;
            }
            catch (Exception ex)
            {
                Main.PrintException(ex);
                return false;
            }
        }
    }
}