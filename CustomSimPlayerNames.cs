using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Erenshor_CustomSimPlayerNames
{
    [BepInPlugin(ModGUID, ModDescription, ModVersion)]
    public class CustomSimPlayerNames : BaseUnityPlugin
    {
        public static CustomSimPlayerNames Instance;

        internal const string ModName = "CustomSimPlayerNames";
        internal const string ModVersion = "1.1.0";
        internal const string ModDescription = "Custom SimPlayer Names";
        internal const string Author = "Brad522";
        private const string ModGUID = Author + "." + ModName;

        private readonly Harmony harmony = new Harmony(ModGUID);

        internal static ManualLogSource Log;
        internal static Random RandomGen;

        public static ConfigEntry<bool> RandomizeNames;
        private static readonly HashSet<string> randomizedFiles = new HashSet<string>();

        public void Awake()
        {
            Instance = this;

            RandomizeNames = Config.Bind(
                "General",
                "RandomizeNames",
                false,
                "If true, SimPlayer names will be assigned randomly instead of in order."
            );

            Log = Logger;
            RandomGen = new Random();
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(SimPlayerMngr))]
        [HarmonyPatch("CreateGenericSimPlayers")]
        public class SimPlayerMngr_CreateGenericSimPlayers_Patch
        {
            [HarmonyPrefix]
            public static void Prefix()
            {
                LoadNamesFromFile("NameDatabaseMale.txt");
                LoadNamesFromFile("NameDatabaseFemale.txt", true);
            }
        }

        private static void LoadNamesFromFile(string fileName = "CustomSimPlayerNames.txt", bool female = false)
        {
            string modDir = Path.GetDirectoryName(Path.GetFullPath(Assembly.GetExecutingAssembly().Location));
            string filePath = Path.Combine(modDir, fileName);

            if (!File.Exists(filePath))
            {
                Log.LogError($"[CustomSimPlayerNames] Name file not found: {filePath}");
                return;
            }

            try
            {
                var lines = File.ReadAllLines(filePath);
                var names = new List<string>();

                foreach (var line in lines)
                {
                    string trimmed = line.Trim();

                    if (string.IsNullOrEmpty(trimmed))
                        continue;

                    string normalized = trimmed.Normalize(NormalizationForm.FormC);

                    if (!IsValidName(normalized))
                    {
                        Log.LogWarning($"[CustomSimPlayerNames] Invalid name skipped: {trimmed}");
                        continue;
                    }

                    if (names.Contains(normalized))
                    {
                        Log.LogWarning($"[CustomSimPlayerNames] Duplicate name skipped: {normalized}");
                        continue;
                    }

                    names.Add(normalized);
                }

                ref List<string> targetDatabase = ref (female
                    ? ref GameData.SimMngr.NameDatabaseFemale
                    : ref GameData.SimMngr.NameDatabaseMale);

                string genderLabel = female ? "Female" : "Male";

                if (names.Count < 60 && targetDatabase != null)
                {
                    int added = 0;
                    foreach (var name in targetDatabase)
                    {
                        if (!names.Contains(name))
                        {
                            names.Add(name);
                            added++;

                            if (names.Count >= 60)
                                break;
                        }
                    }

                    Log.LogDebug($"[CustomSimPlayerNames] Added {added} names from existing database to {fileName}");
                }

                if (RandomizeNames.Value)
                {
                    string backupPath = Path.Combine(modDir, Path.GetFileNameWithoutExtension(fileName) + "_original.txt");
                    
                    if (!File.Exists(backupPath))
                    {
                        File.Copy(filePath, backupPath);
                        Log.LogDebug($"[CustomSimPlayerNames] Created backup of original names file at {backupPath}");
                    }
                    else
                    {
                        Log.LogWarning($"[CustomSimPlayerNames] Backup file already exists: {backupPath}");
                    }

                    ShuffleNames(names);
                    File.WriteAllLines(filePath, names);
                    Log.LogDebug($"[CustomSimPlayerNames] Shuffled names and saved to {filePath}");

                    MarkRandomizationCompleteIfNeeded(fileName);
                }

                Log.LogDebug($"[CustomSimPlayerNames] Loaded {names.Count} names from {filePath}");
                targetDatabase = names;
            } catch (IOException ex)
            {
                Log.LogError($"[CustomSimPlayerNames] Error reading name file: {ex.Message}");
            }
        }

        private static readonly HashSet<string> ReservedWindowsNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        private static bool IsValidName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return false;

            if (name.EndsWith(" ") || name.EndsWith("."))
                return false;

            if (ReservedWindowsNames.Contains(name))
                return false;

            if (name.Length > 30)
                return false;

            if (name.Any(c => char.IsControl(c)))
                return false;

            return true;
        }

        private static void ShuffleNames(List<string> names)
        {
            int n = names.Count;

            while (n > 1)
            {
                n--;
                int k = RandomGen.Next(n + 1);
                (names[n], names[k]) = (names[k], names[n]);
            }

            Log.LogDebug("[CustomSimPlayerNames] Names shuffled.");
        }

        private static void MarkRandomizationCompleteIfNeeded(string justRandomized)
        {
            randomizedFiles.Add(justRandomized);

            if (randomizedFiles.Contains("NameDatabaseMale.txt") &&
                randomizedFiles.Contains("NameDatabaseFemale.txt"))
            {
                Log.LogInfo("[CustomSimPlayerNames] All name databases have been randomized.");
                RandomizeNames.Value = false;
                Instance.Config.Save();

                randomizedFiles.Clear();
            }
        }
    }
}
