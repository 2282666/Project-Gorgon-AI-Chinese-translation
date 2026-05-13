using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace GorgonChinesePatch
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class ChinesePatchPlugin : BasePlugin
    {
        public const string PluginGuid = "com.gorgon.chinesepatch";
        public const string PluginName = "GorgonChinesePatch";
        public const string PluginVersion = "1.0.0";

        public static Harmony HarmonyInstance { get; private set; }
        public static TranslationManager TranslationMgr { get; private set; }
        public static ChatFilter ChatFilterMgr { get; private set; }
        public static TextCollector TextCollectorMgr { get; private set; }
        public static AiTranslationManager AiManager { get; private set; }
        public new static ManualLogSource Log { get; private set; }

        public static Type TextType { get; private set; }
        public static Type TMPTextType { get; private set; }
        public static Type GameObjectType { get; private set; }
        public static Type TransformType { get; private set; }

        public override void Load()
        {
            Log = base.Log;
            Console.OutputEncoding = Encoding.UTF8;
            Log.LogInfo("GorgonChinesePatch 正在加载...");

            string pluginPath = Path.Combine(Paths.PluginPath, "ChinesePatch");
            Directory.CreateDirectory(pluginPath);

            string aiConfigPath = Path.Combine(pluginPath, "ai_config.json");
            string textsPath = Path.Combine(pluginPath, "collected_texts.json");
            string translationsPath = Path.Combine(pluginPath, "translations.json");

            AiManager = AiTranslationManager.Instance;
            AiManager.Initialize(aiConfigPath, Log);

            TextCollectorMgr = new TextCollector(textsPath, translationsPath, Log);
            ChatFilterMgr = new ChatFilter(Log);
            TranslationMgr = new TranslationManager(translationsPath, Log, TextCollectorMgr, AiManager);

            if (AiManager.IsConfigured && AiManager.AutoTranslate)
            {
                TranslationMgr.EnableAutoTranslate();
            }

            InitializeUnityTypes();

            HarmonyInstance = new Harmony(PluginGuid);

            if (TextType != null)
            {
                try
                {
                    var setTextMethod = TextType.GetMethod("set_text", BindingFlags.Instance | BindingFlags.Public);
                    if (setTextMethod != null)
                    {
                        var prefix = new HarmonyMethod(typeof(TextPatch), nameof(TextPatch.Prefix));
                        HarmonyInstance.Patch(setTextMethod, prefix);
                        Log.LogInfo("Text 补丁应用成功");
                    }
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"Text 补丁失败: {ex.Message}");
                }
            }

            if (TMPTextType != null)
            {
                try
                {
                    var setTextMethod = TMPTextType.GetMethod("set_text", BindingFlags.Instance | BindingFlags.Public);
                    if (setTextMethod != null)
                    {
                        var prefix = new HarmonyMethod(typeof(TMPTextPatch), nameof(TMPTextPatch.Prefix));
                        HarmonyInstance.Patch(setTextMethod, prefix);
                        Log.LogInfo("TMP_Text 补丁应用成功");
                    }
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"TMP_Text 补丁失败: {ex.Message}");
                }
            }

            Log.LogInfo("GorgonChinesePatch 加载完成！" + TranslationMgr.GetStats());
        }

        private void InitializeUnityTypes()
        {
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var name = assembly.GetName().Name;
                    if (name == "UnityEngine.UI" && TextType == null)
                    {
                        TextType = assembly.GetType("UnityEngine.UI.Text");
                    }
                    else if (name == "Unity.TextMeshPro" && TMPTextType == null)
                    {
                        TMPTextType = assembly.GetType("TMPro.TMP_Text");
                    }
                    else if (name == "UnityEngine.CoreModule")
                    {
                        if (GameObjectType == null)
                            GameObjectType = assembly.GetType("UnityEngine.GameObject");
                        if (TransformType == null)
                            TransformType = assembly.GetType("UnityEngine.Transform");
                    }
                }

                if (TextType != null)
                    Log.LogInfo($"找到 Text 类型: {TextType.FullName}");
                else
                    Log.LogWarning("未找到 Text 类型");

                if (TMPTextType != null)
                    Log.LogInfo($"找到 TMP_Text 类型: {TMPTextType.FullName}");
                else
                    Log.LogWarning("未找到 TMP_Text 类型");
            }
            catch (Exception ex)
            {
                Log.LogError($"初始化 Unity 类型失败: {ex.Message}");
            }
        }

        public static string GetGameObjectPath(object obj)
        {
            if (obj == null || TransformType == null)
                return "";

            try
            {
                var nameProp = obj.GetType().GetProperty("name");
                string path = nameProp?.GetValue(obj)?.ToString() ?? "";

                var transformProp = obj.GetType().GetProperty("transform");
                var transform = transformProp?.GetValue(obj);

                while (transform != null)
                {
                    var parentProp = transform.GetType().GetProperty("parent");
                    var parent = parentProp?.GetValue(transform);
                    if (parent == null)
                        break;

                    var parentNameProp = parent.GetType().GetProperty("name");
                    path = (parentNameProp?.GetValue(parent)?.ToString() ?? "") + "/" + path;

                    transform = parent;
                }

                return path;
            }
            catch
            {
                return "";
            }
        }
    }

    public static class TextPatch
    {
        public static void Prefix(object __instance, ref string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            try
            {
                if (ShouldTranslate(__instance, value))
                {
                    value = ChinesePatchPlugin.TranslationMgr.Translate(value);
                }
            }
            catch
            {
            }
        }

        static bool ShouldTranslate(object textComponent, string newText)
        {
            try
            {
                var gameObjectProp = textComponent.GetType().GetProperty("gameObject");
                var gameObject = gameObjectProp?.GetValue(textComponent);
                if (gameObject == null)
                    return false;

                string path = ChinesePatchPlugin.GetGameObjectPath(gameObject);

                return ChinesePatchPlugin.ChatFilterMgr.ShouldTranslate(path, newText);
            }
            catch
            {
                return false;
            }
        }
    }

    public static class TMPTextPatch
    {
        public static void Prefix(object __instance, ref string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            try
            {
                if (ShouldTranslate(__instance, value))
                {
                    value = ChinesePatchPlugin.TranslationMgr.Translate(value);
                }
            }
            catch
            {
            }
        }

        static bool ShouldTranslate(object textComponent, string newText)
        {
            try
            {
                var gameObjectProp = textComponent.GetType().GetProperty("gameObject");
                var gameObject = gameObjectProp?.GetValue(textComponent);
                if (gameObject == null)
                    return false;

                string path = ChinesePatchPlugin.GetGameObjectPath(gameObject);

                return ChinesePatchPlugin.ChatFilterMgr.ShouldTranslate(path, newText);
            }
            catch
            {
                return false;
            }
        }
    }
}
