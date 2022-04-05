using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using HarmonyLib;

namespace COM3D2.AutoSave
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class AutoSave : BaseUnityPlugin
    {
        internal static ManualLogSource logger;
        internal static Slot slot = new();

        private static ConfigEntry<bool> enableAutoSave;
        private static ConfigEntry<bool> useMultipleSaveSlots;
        private static ConfigEntry<int> startingSaveSlot;
        private static ConfigEntry<int> endingSaveSlot;
        private static ConfigEntry<int> lastUsedSlot;
        private static int currentDay = 0;
        private static bool isNewDay = false;


        private void Awake()
        {
            //Config
            enableAutoSave = Config.Bind("Config", "Enable", false, "Enable AutoSave");
            useMultipleSaveSlots = Config.Bind("Config", "Multiple Save", true, "Save Across multiple save slots, in order");
            startingSaveSlot = Config.Bind("Config", "First Slot", 90, "First AutoSave slot (0 to 99");
            endingSaveSlot = Config.Bind("Config", "Last Slot", 99, "Last AutoSave slot (0 to 99)");
            lastUsedSlot = Config.Bind("Plugin State", "Last Used Slot", 90, "");


            //BepinEx Logger
            logger = Logger;

            //Harmony
            Harmony.CreateAndPatchAll(typeof(AutoSave));
        }

        public void Update()
        {
        }

        private void Main()
        {
            // Event Management
            SceneManager.sceneLoaded += OnSceneLoaded;
            useMultipleSaveSlots.SettingChanged += UpdateConfig;
        }

        private void UpdateConfig(object sender, EventArgs e)
        {
            slot.Check();
        }


        private static void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
            // Scene 3 is the main desktop scene.
            if (scene.buildIndex == 3)
            {
                if (isNewDay && enableAutoSave.Value && CheckIsAtDesktop())
                {
                    isNewDay = false;
                    slot.SaveData();
                }
            }
        }

        // Look for an active schedule button, which only appears during daytime when at your desk
        private static bool CheckIsAtDesktop()
        {
            GameObject scheduleButton = GameObject.Find("Schedule");

            bool isDesktop = scheduleButton != null && scheduleButton.active;
            //logger.LogWarning("At Desktop");
            return isDesktop;
        }

        // Triggers On new Day.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameMain), nameof(GameMain.OnStartDay))]
        public static void OnStartDayPostfix()
        {
            logger.LogWarning("It's a new Day!");
            isNewDay = true;
        }



        internal class Slot
        {
            internal void Check()
            {
                if (startingSaveSlot.Value < 0) { startingSaveSlot.Value = 0; }
                if (endingSaveSlot.Value > 99) { endingSaveSlot.Value = 99; }

                if (lastUsedSlot.Value > endingSaveSlot.Value || lastUsedSlot.Value < startingSaveSlot.Value)
                {
                    lastUsedSlot.Value = startingSaveSlot.Value;
                }
            }

            internal int GetNext()
            {
                // starting slot will be used if Multiple Save isn't enabled
                if (!useMultipleSaveSlots.Value) { return startingSaveSlot.Value; }

                // returns the next slot to save on
                int slot = lastUsedSlot.Value + 1;
                if (slot > endingSaveSlot.Value)
                {
                    slot = startingSaveSlot.Value;
                }
                lastUsedSlot.Value = slot;
                return slot;
            }


            internal void SaveData(string comment = "Autosave")
            {
                int slot = GetNext();

                GameMain.Instance.Serialize(slot, comment);
                logger.LogMessage($"Autosave completed on slot {slot}");
            }
        }
    }
}
