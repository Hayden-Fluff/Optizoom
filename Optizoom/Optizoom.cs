using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
using System;
using BaseX;
using System.Collections.Generic;

namespace Optizoom
{
    public class Optizoom : NeosMod
    {
        public override string Name => "Optizoom";
        public override string Author => "badhaloninja & Hayden";
        public override string Version => "1.2.0";
        public override string Link => "https://github.com/Hayden-Fluff/Optizoom";


        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> Enabled =
            new ModConfigurationKey<bool>("Enabled", "Enable Optizoom", () => true);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<Key> ZoomKey =
            new ModConfigurationKey<Key>("keyBind", "Zoom Key", () => Key.Z);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<float> ZoomFOV =
            new ModConfigurationKey<float>("zoomFOV", "Zoom FOV", () => 20f);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> UseMouse =
            new ModConfigurationKey<bool>("useMouse", "Use Mouse Button", () => true);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<MouseButton> Button =
            new ModConfigurationKey<MouseButton>("MouseButton", "Mouse Button", () => MouseButton.Button4);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> ZoomToggle =
            new ModConfigurationKey<bool>("ZoomToggled", "Zoom Toggle", () => false);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> AllowSecondaryConflict =
            new ModConfigurationKey<bool>("AllowSecondaryConflict", "Allow Secondary Conflict", () => false);


        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> LerpZoom =
            new ModConfigurationKey<bool>("lerpZoom", "Lerp Zoom", () => true);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<float> ZoomSpeed =
            new ModConfigurationKey<float>("zoomSpeed", "Zoom Speed", () => 50f);

        private static ModConfiguration config;

        public override void OnEngineInit()
        {
            config = GetConfiguration();

            Harmony harmony = new Harmony("me.Hayden.Optizoom");
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(UserRoot), "get_DesktopFOV")]
        class Optizoom_Patch
        {
            static Dictionary<UserRoot, UserRootFOVLerps> lerps = new Dictionary<UserRoot, UserRootFOVLerps>();
            
            private static double _lastSetTime;
            private static bool zoom;

            public static void Postfix(UserRoot __instance, ref float __result)
            {
                if (!lerps.TryGetValue(__instance, out UserRootFOVLerps lerp))
                {
                    lerp = new UserRootFOVLerps(); // Needs one per UserRoot or else userspace and focused world fights
                    lerps.Add(__instance, lerp);
                }
                if (config == null) return;

                bool zoomKeySet = config.GetValue(UseMouse)
                    ? __instance.InputInterface.Mouse[config.GetValue(Button)].Held 
                    : __instance.InputInterface.GetKey(config.GetValue(ZoomKey));

                bool zoomKeyToggle = config.GetValue(UseMouse)
                    ? __instance.InputInterface.Mouse[config.GetValue(Button)].Pressed
                    : __instance.InputInterface.GetKeyDown(config.GetValue(ZoomKey));

                bool otherChecks = !__instance.LocalUser.HasActiveFocus() // Not focused in any field
                    && !Userspace.HasFocus // Not focused in userspace field
                    && __instance.Engine.WorldManager.FocusedWorld == __instance.World; // Focused in the same world as the UserRoot

                Chirality userChiraliry = Settings.ReadValue("Settings.Input.User.PrimaryHand", Chirality.Right);
                User localUser = __instance.LocalUser;
                UserRoot root = localUser.Root;
                CommonTool currentCommonTool = root.GetRegisteredComponent((CommonTool c) => (Chirality)c.Side == userChiraliry);
                IToolTip? currentToolTip = currentCommonTool.ActiveToolTip; // Don't try to log this, it will throw an NRE and crash the world!!!
                bool hasToolTip = currentToolTip != null; // Check if a tool is in use, thanks for the help on this one Cyro
                bool secondaryConflict = config.GetValue(UseMouse)
                        ? config.GetValue(Button) == MouseButton.Button4
                        : config.GetValue(ZoomKey) == Key.R;

                if (config.GetValue(ZoomToggle))
                {

                    if (zoomKeyToggle && _lastSetTime != __instance.World.Time.WorldTime && otherChecks)
                    {
                        _lastSetTime = __instance.World.Time.WorldTime;
                        Msg("Hey, you pressed the zoom button at " + __instance.World.Time.WorldTime +  " in" + __instance.World.Name);
                        zoom = !zoom;
                    }
                }
                else
                {
                    zoom = zoomKeySet;
                }

                var flag = secondaryConflict & !config.GetValue(AllowSecondaryConflict)
                        ? !hasToolTip && zoom
                        : zoom
                    & otherChecks
                    && config.GetValue(Enabled);
                
                //if (__instance.InputInterface.Mouse[config.GetValue(Button)].Pressed && otherChecks)
                //{
                //    Msg("Zoom:" + zoom,
                //        "Zoom Key Set:" + zoomKeySet,
                //        "Zoom Key Toggle:" + zoomKeyToggle,
                //        "Other Checks:" + otherChecks,
                //        "Secondary Conflict:" + secondaryConflict,
                //        __instance.World,
                //        currentToolTip?.GetType()); // This will crash
                //}

                float target = flag ? Settings.ReadValue("Settings.Graphics.DesktopFOV", 60f) - config.GetValue(ZoomFOV) : 0f;//__result;

                if (config.GetValue(LerpZoom))
                {
                    lerp.currentLerp = MathX.SmoothDamp(lerp.currentLerp, target, ref lerp.lerpVelocity, config.GetValue(ZoomSpeed), 179f, __instance.Time.Delta); // Funny lerp
                    __result -= lerp.currentLerp;
                } 
                
                else
                {
                    __result -= target;
                }

                __result = MathX.FilterInvalid(__result, 60f); // fallback to 60 fov if invalid
                __result = MathX.Clamp(__result, 1f, 179f);

                //Msg($"{__instance.World.Name}: {__instance.ActiveUser.UserID} - {flag} | {lerp.lerpVelocity} | {__result}");
            }
        }

        class UserRootFOVLerps
        {
            public float currentLerp = 0f;
            public float lerpVelocity = 0f;
        }
    }
}