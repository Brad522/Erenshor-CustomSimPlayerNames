using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Erenshor_CustomSimPlayerNames
{
    [BepInPlugin(ModGUID, ModDescription, ModVersion)]
    public class CustomSimPlayerNames : BaseUnityPlugin
    {
        internal const string ModName = "CustomSimPlayerNames";
        internal const string ModVersion = "1.0.0";
        internal const string ModDescription = "Custom SimPlayer Names";
        internal const string Author = "Brad522";
        private const string ModGUID = Author + "." + ModName;

        private readonly Harmony harmony = new Harmony(ModGUID);

        internal static ManualLogSource Log;

        public void Awake()
        {
            Log = Logger;
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(SimPlayerMngr))]
        [HarmonyPatch("CreateGenericSimPlayers")]
        public class SimPlayerMngr_CreateGenericSimPlayers_Patch
        {
            [HarmonyPrefix]
            public static void Prefix()
            {
                ReplaceDatabaseWithFileNames("NameDatabaseMale.txt");
                ReplaceDatabaseWithFileNames("NameDatabaseFemale.txt", true);
            }
        }

        private static void ReplaceDatabaseWithFileNames(string file = "CustomSimPlayerNames.txt", bool female = false)
        {
            var newNames = LoadNamesFromFile(file);

            if (newNames == null) return;

            ref List<string> targetDatabase = ref (female
                ? ref GameData.SimMngr.NameDatabaseFemale
                : ref GameData.SimMngr.NameDatabaseMale);

            string genderLabel = female ? "Female" : "Male";

            if (newNames.Count < 50 && targetDatabase != null)
            {
                foreach (var name in targetDatabase)
                {
                    if (!newNames.Contains(name))
                    {
                        newNames.Add(name);
                    }
                }
            }

            targetDatabase = newNames;

            Log.LogDebug($"[CustomSimPlayerNames] Replaced NameDatabase{genderLabel} with {newNames.Count} names from {file}");
        }

        private static List<string> LoadNamesFromFile(string fileName = "CustomSimPlayerNames.txt")
        {
            string modDir = Path.GetDirectoryName(Path.GetFullPath(Assembly.GetExecutingAssembly().Location));
            string filePath = Path.Combine(modDir, fileName);

            if (!File.Exists(filePath))
            {
                Log.LogError($"[CustomSimPlayerNames] Name file not found: {filePath}");
                return null;
            }

            try
            {
                var lines = File.ReadAllLines(filePath);
                var names = new List<string>();
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        names.Add(trimmed);
                    }
                }
                Log.LogDebug($"[CustomSimPlayerNames] Loaded {names.Count} names from {filePath}");
                return names;
            } catch (IOException ex)
            {
                Log.LogError($"[CustomSimPlayerNames] Error reading name file: {ex.Message}");
                return null;
            }
        }
    }
}
