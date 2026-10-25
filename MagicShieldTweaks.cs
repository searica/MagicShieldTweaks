// Ignore Spelling: ToolTweaks Jotunn

using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Extensions;
using Jotunn.Managers;
using Jotunn.Utils;
using Logging;

using System;

using System.Reflection;

using UnityEngine;


namespace MagicShieldTweaks
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid, Jotunn.Main.Version)]
    [NetworkCompatibility(CompatibilityLevel.VersionCheckOnly, VersionStrictness.Patch)]
    [SynchronizationMode(AdminOnlyStrictness.IfOnServer)]
    internal sealed class MagicShieldTweaks : BaseUnityPlugin
    {
        internal const string Author = "Searica";
        public const string PluginName = "FoodTweaks";
        public const string PluginGUID = $"{Author}.Valheim.{PluginName}";
        public const string PluginVersion = "0.1.0";

        // Use this class to add your own localization to the game
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        //public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        private const string MainSection = "FoodTweaks";

        internal static MagicShieldTweaks Instance;
        private static ConfigFileWatcher ConfigWatcher;


        internal ConfigEntry<bool> AddStaffSecondaryAttacks;
        internal ConfigEntry<bool> ChangeShieldColorByHealth;
        internal ConfigEntry<Color> ShieldTargetColorZeroHealth;
        internal ConfigEntry<bool> PreventZeroDamageShieldSpam;
        internal ConfigEntry<bool> PreventLookVectorConsoleSpam;
        

        private bool ShouldUpdatePlugin = false;

        public void Awake()
        {
            Instance = this;
            Log.Init(Logger);
            SetUpConfigEntries();

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGUID);
            Game.isModded = true;

            ConfigWatcher = new ConfigFileWatcher(Config);
            ConfigWatcher.OnConfigFileReloaded += () => { UpdatePlugin(shouldSave: false); };
            SynchronizationManager.OnConfigurationWindowClosed += () => { UpdatePlugin(shouldSave: true); };
        }

        public void OnDestroy()
        {
            Config.Save();
        }

        internal void SetUpConfigEntries()
        {
            Config.SaveOnConfigSet = false;

            Log.Verbosity = Config.BindConfigInOrder(
                MainSection,
                "Verbosity",
                Log.InfoLevel.Low,
                "Low will log basic information about the mod. Medium will log information that " +
                "is useful for troubleshooting. High will log a lot of information, do not set " +
                "it to this without good reason as it will slow Down your game.",
                synced: false,
                sectionOrder: false
            );

            AddStaffSecondaryAttacks = Config.BindConfigInOrder(
                MainSection,
                nameof(AddStaffSecondaryAttacks),
                true,
                "Whether to add secondary attacks to staffs that provide a shield-like status effect that allows you to remove the status effect.",
                sectionOrder: false,
                synced: false
            );

            ChangeShieldColorByHealth = Config.BindConfigInOrder(
                MainSection,
                nameof(ChangeShieldColorByHealth),
                true,
                "Whether to change the color of your shield based on its current health.",
                sectionOrder: false,
                synced: false
            );

            ShieldTargetColorZeroHealth = Config.BindConfigInOrder(
                MainSection,
                nameof(ShieldTargetColorZeroHealth),
                new Color(0f, 0f, 1f, 0.5f),
                "Shield will have this color at 0 health.",
                sectionOrder: false,
                synced: false
            );

            PreventZeroDamageShieldSpam = Config.BindConfigInOrder(
                MainSection,
                nameof(PreventZeroDamageShieldSpam),
                true,
                "If you take 0 damage to your shield then prevent the hit effect from playing. Could prevent occasional hit effect spam.",
                sectionOrder: false,
                synced: false
            );
            PreventLookVectorConsoleSpam = Config.BindConfigInOrder(
                MainSection,
                nameof(PreventLookVectorConsoleSpam),
                true,
                "If shield takes damage from an indirect hit then the 'Prevent Look Rotation Viewing Vector Is Zero' will no longer be shown in log file or console.",
                sectionOrder: false,
                synced: false
            );

            
            AddStaffSecondaryAttacks.SettingChanged += SetUpdatePlugin;
            ChangeShieldColorByHealth.SettingChanged += SetUpdatePlugin;
            ShieldTargetColorZeroHealth.SettingChanged += SetUpdatePlugin;
            PreventZeroDamageShieldSpam.SettingChanged += SetUpdatePlugin;
            PreventLookVectorConsoleSpam.SettingChanged += SetUpdatePlugin;

            Config.Save();
        }

        private static void SetUpdatePlugin(object obj, EventArgs e)
        {
            Instance.ShouldUpdatePlugin = !Instance.ShouldUpdatePlugin | Instance.ShouldUpdatePlugin;
        }

        private static void UpdatePlugin(bool shouldSave)
        {
            if (Instance.ShouldUpdatePlugin)
            {

                Instance.ShouldUpdatePlugin = false;
                if (shouldSave)
                {
                    Instance.Config.Save();
                }
            }
        }
    }
}