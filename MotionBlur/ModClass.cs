using HutongGames.PlayMaker.Actions;
using Modding;
using Modding.Converters;
using Modding.Menu;
using Modding.Menu.Config;
using Newtonsoft.Json;
using Satchel;
using Satchel.BetterMenus;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UObject = UnityEngine.Object;

namespace MotionBlur
{
    public class GlobalSettings
    {
        public float ShutterAngle = 1080f;
        public int SampleCount = 1000;
        public float FrameBlendingStrength = 1f;
        public int FrameCount = 8;
        public bool Enabled = true;
    }

    public class MotionBlur : Mod, ICustomMenuMod, IGlobalSettings<GlobalSettings>
    {
        internal static MotionBlur Instance;
        public override string GetVersion() => "1.0.1.0";

        public static GlobalSettings GS = new GlobalSettings();
        #region Save/Load settings
        public void OnLoadGlobal(GlobalSettings s)
        {
            GS = s;
        }
        public GlobalSettings OnSaveGlobal()
        {
            return GS;
        }
        #endregion Save/Load settings

        Camera cam;
        AssetBundle shadersBundle;
        Shader reconstruction;
        Shader frameBlending;
        Kino.Motion motionBlur;
        
        public override void Initialize()
        {
            Log("Initializing");
            Instance = this;

            LoadShadersFromBundle();

            if(GS.Enabled)
            {
                ModHooks.AfterSavegameLoadHook += AfterSaveLoaded;

                Enable();
            }

            Log("Initialized");
        }

        void Disable()
        {
            cam = Camera.main;
            cam.gameObject.RemoveComponent<Kino.Motion>();
        }

        void Enable()
        {
            cam = Camera.main;

            if(cam.gameObject.GetComponent<Kino.Motion>() != null) return;

            motionBlur = cam.gameObject.AddComponent<Kino.Motion>();
            motionBlur.Init(reconstruction, frameBlending, GS.FrameCount);
            motionBlur.shutterAngle = GS.ShutterAngle;
            motionBlur.sampleCount = GS.SampleCount;
            motionBlur.frameBlending = GS.FrameBlendingStrength;
        }
        
        void LoadShadersFromBundle()
        {
            var assembly = Assembly.GetExecutingAssembly();

            string resourceName = "MotionBlur.Bundles.kinoshaders";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Log("Could not find: " + resourceName);
                    return;
                }

                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);

                shadersBundle = AssetBundle.LoadFromMemory(data);
                Log("Bundle uploaded from memory succesfully");
            }

            Material[] materials = shadersBundle.LoadAllAssets<Material>();
            
            foreach (var mat in materials)
            {
                Log($"Material isn't found: {mat.name}");

                if (mat.name.Contains("Reconstruction"))
                {
                    reconstruction = mat.shader;
                    Log("Reconstruction is loaded");
                }
                else if (mat.name.Contains("FrameBlending"))
                {
                    frameBlending = mat.shader;
                    Log("FrameBlending is loaded");
                }
            }

            if (reconstruction == null) Log("Reconstruction isn't found");
            if (frameBlending == null) Log("FrameBlending isn't found");

            shadersBundle.Unload(false);
            
        }
        private Menu MenuRef;
        public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? modtoggledelegates) 
        {
            if (MenuRef == null)
            {
                int _fontSize = 30;
                MenuRef = new Menu("MotionBlur", new Element[]
                {
                    new HorizontalOption
                    (
                        "Mod",
                        "",
                        new[] {"Enabled", "Disabled" },
                        index =>
                        {
                            GS.Enabled = index == 0;
                            if(GS.Enabled)
                            {
                                ModHooks.AfterSavegameLoadHook += AfterSaveLoaded;
                                Enable();
                            }
                            else
                            {
                                ModHooks.AfterSavegameLoadHook -= AfterSaveLoaded;
                                Disable();
                            }
                        },
                        () => GS.Enabled ? 0 : 1

                    ),
                    new TextPanel(name: "The angle of rotary shutter. Larger values give longer exposure.", fontSize: _fontSize, width: 1200),
                    Blueprints.FloatInputField
                    (
                        "Shutter Angle",
                        angle =>
                        {
                            angle = Mathf.Clamp(angle, 0f, 10000000f);
                            GS.ShutterAngle = angle;
                            if(cam == null) return;
                            motionBlur.shutterAngle = GS.ShutterAngle;
                        },
                        () => GS.ShutterAngle,
                        _characterLimit: 8,
                        _config: new Satchel.BetterMenus.Config.InputFieldConfig
                        {
                            inputBoxWidth = 240f,
                            fontSize = 46
                        }
                    ),
                    new TextPanel(name: "The amount of sample points, which affects quality and performance.", fontSize: _fontSize, width: 1400),
                    Blueprints.IntInputField
                    (
                        "Sample Count",
                        count =>
                        {
                            count = Mathf.Clamp(count, 1, 10000000);
                            GS.SampleCount = count;
                            if(cam == null) return;
                            motionBlur.sampleCount = GS.SampleCount;
                        },
                        () => GS.SampleCount,
                        _characterLimit: 8,
                        _config: new Satchel.BetterMenus.Config.InputFieldConfig
                        {
                            inputBoxWidth = 240f,
                            fontSize = 46
                        }
                    ),
                    new TextPanel(name: "How many frames will be stored in video memory.", fontSize: _fontSize),
                    Blueprints.IntInputField
                    (
                        "Frame Count",
                        count =>
                        {
                            count = Mathf.Clamp(count, 1, 10000);
                            GS.FrameCount = count;
                            Disable();
                            Enable();
                        },
                        () => GS.FrameCount
                    ),
                    new CustomSlider (
                        name: "Frame Blending Strength",
                        storeValue: val =>
                        {
                            GS.FrameBlendingStrength = val;
                            if(cam == null) return;
                            motionBlur.frameBlending = GS.FrameBlendingStrength;
                        },
                        loadValue: () => GS.FrameBlendingStrength,
                        minValue: 0,
                        maxValue: 1
                        ),

                });
            }
            return MenuRef.GetMenuScreen(modListMenu);
        }
        public bool ToggleButtonInsideMenu => true;

        public void AfterSaveLoaded(SaveGameData saveData)
        {
            Enable();
        }
    }
}