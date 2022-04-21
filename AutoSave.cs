using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using HarmonyLib;
using System.Collections;

namespace COM3D2.AutoSave
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class AutoSave : BaseUnityPlugin
    {
        internal static ManualLogSource logger;
        internal static AutoSave instance;
        internal static Slot slot = new();

        private static ConfigEntry<bool> enableAutoSave;
        private static ConfigEntry<bool> useMultipleSaveSlots;
        private static ConfigEntry<int> startingSaveSlot;
        private static ConfigEntry<int> endingSaveSlot;

        private static ConfigEntry<bool> enableAutoLoad;

        private static ConfigEntry<int> lastUsedSlot;
        private static ConfigEntry<bool> isInit;


        private bool isNewDay = false;
        private bool isOverride = false;


        private void Awake()
        {
            //Config
            //AutoSave
            enableAutoSave = Config.Bind("AutoSave", "Enable", false, "Enable AutoSave");
            useMultipleSaveSlots = Config.Bind("AutoSave", "Multiple Save", true, "Save Across multiple save slots, in numerical order");
            startingSaveSlot = Config.Bind("AutoSave", "First Slot", 90, new ConfigDescription("First AutoSave slot", new AcceptableValueRange<int>(0, 98)));
            endingSaveSlot = Config.Bind("AutoSave", "Last Slot", 99, new ConfigDescription("Last AutoSave slot", new AcceptableValueRange<int>(1, 99)));
            //AutoLoad
            enableAutoLoad = Config.Bind("AutoLoad", "Enable AutoLoad", false, "AutoLoad the latest Save on Game Start, maintain shift to override");
            //Advanced
            lastUsedSlot = Config.Bind("Plugin State", "Last Used Slot", 90, new ConfigDescription("", new AcceptableValueRange<int>(0, 99), "Advanced"));
            isInit = Config.Bind("Plugin State", "isInit", false, new ConfigDescription("Whether or not the plugin has already been ran once", null, "Advanced"));


            //BepinEx Logger
            logger = Logger;

            // 
            instance = this;

            //Harmony
            Harmony.CreateAndPatchAll(typeof(AutoSave));

            //Event Management
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (enableAutoLoad.Value)
            { 
                SceneManager.sceneLoaded += AutoLoad; 
            }
            if (!isInit.Value)
            {
                Init init = new();
                SceneManager.sceneLoaded += init.OnSceneLoaded; 
            }            
            useMultipleSaveSlots.SettingChanged += UpdateConfig;
        }

        private void Update()
        {
            isOverride = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
        }

        private void UpdateConfig(object sender, EventArgs e)
        {
            slot.Check();
        }


        private void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
            // Scene 3 is the main desktop scene.
            if (scene.buildIndex == 3)
            {
                if (isNewDay && enableAutoSave.Value && IsAtDesktop())
                {
                    isNewDay = false;
                    slot.SaveData();
                }
            }
        }

        private void AutoLoad(Scene scene, LoadSceneMode sceneMode)
        {
            // scene 15 is the warning screen
            // loads the game and unsubscribes to the event
            // overriden is the user press Shift
            if (scene.buildIndex == 15)
            {
                SceneManager.sceneLoaded -= AutoLoad;
                if (!isOverride)
                {
                    slot.LoadLast();
                }
            }
        }


        // Look for an active schedule button, which only appears during daytime when at your desk
        private bool IsAtDesktop()
        {
            GameObject scheduleButton = GameObject.Find("Schedule");

            bool isDesktop = scheduleButton != null && scheduleButton.activeSelf;
            return isDesktop;
        }

        // Triggers On new Day.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameMain), nameof(GameMain.OnStartDay))]
        public static void OnStartDayPostfix()
        {
            logger.LogInfo("It's a new Day!");
            instance.isNewDay = true;
        }

        /// <summary>
        /// Save slot related operations
        /// </summary>
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

            private int GetNext()
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

            private int GetLast()
            {
                return (int)GameMain.Instance.m_lNewSaveDataNo;
            }


            internal void SaveData(string comment = "AutoSave")
            {
                int slot = GetNext();

                GameMain.Instance.Serialize(slot, comment);
                logger.LogMessage($"Autosave completed on slot {slot}");
            }

            internal void LoadLast()
            {
                int lastSlot = GetLast();
                GameMain.Instance.Deserialize(lastSlot);
            }
        }

        /// <summary>
        /// Everything needed to display the starting warning Dialogue Box
        /// </summary>
        internal class Init
        {
            internal void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
            {
                if (scene.name == "SceneTitle")
                {
                    instance.StartCoroutine(this.Warning());
                    SceneManager.sceneLoaded -= OnSceneLoaded;
                }
            }

            internal IEnumerator Warning()
            {
                yield return new WaitForSeconds(3f);
                while (GameMain.Instance.SysDlg.isActiveAndEnabled)
                {
                    yield return new WaitForSeconds(2f);
                }
                GameMain.Instance.SysDlg.Show("Do you want to enable AutoSaves?\nWARNING: This will use slots 90 to 99 by default.",
                    SystemDialog.TYPE.YES_NO,
                    delegate { Finish(true); },
                    delegate { Finish(false); });
            }

            internal void Finish(bool answer)
            {
                enableAutoSave.Value = answer;
                isInit.Value = true;
                GameMain.Instance.SysDlg.Close();
            }
        }
    }
}
