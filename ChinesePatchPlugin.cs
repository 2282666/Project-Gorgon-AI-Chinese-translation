using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.IO;
using System.Linq;
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
        public static Type UnityObjectType { get; private set; }

        private static DateTime _lastScanTime = DateTime.MinValue;
        private static readonly object _scanLock = new object();
        private static bool _scanRequested;

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

            PatchTextSetText();
            PatchTMPTextSetText();
            PatchTMPTextSetTextMethod();

            _scanRequested = true;

            Log.LogInfo("GorgonChinesePatch 加载完成！" + TranslationMgr.GetStats());
        }

        /// <summary>
        /// 为 UnityEngine.UI.Text 的 set_text 属性添加钩子
        /// </summary>
        private void PatchTextSetText()
        {
            if (TextType == null) return;

            try
            {
                var textProp = TextType.GetProperty("text");
                var setTextMethod = textProp?.GetSetMethod(true);
                if (setTextMethod != null)
                {
                    Log.LogInfo($"找到 Text.set_text 方法: {setTextMethod.DeclaringType?.Name}");
                    var prefix = new HarmonyMethod(typeof(TextPatch), nameof(TextPatch.Prefix));
                    HarmonyInstance.Patch(setTextMethod, prefix);
                    Log.LogInfo("Text.set_text 补丁应用成功");
                }
                else
                {
                    Log.LogWarning("未找到 Text.set_text 方法");
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Text.set_text 补丁失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 为 TMPro.TMP_Text 的 set_text 属性添加钩子
        /// </summary>
        private void PatchTMPTextSetText()
        {
            if (TMPTextType == null) return;

            try
            {
                var textProp = TMPTextType.GetProperty("text");
                var setTextMethod = textProp?.GetSetMethod(true);
                if (setTextMethod != null)
                {
                    Log.LogInfo($"找到 TMP_Text.set_text 方法: {setTextMethod.DeclaringType?.Name}");
                    var prefix = new HarmonyMethod(typeof(TMPTextPatch), nameof(TMPTextPatch.Prefix));
                    HarmonyInstance.Patch(setTextMethod, prefix);
                    Log.LogInfo("TMP_Text.set_text 补丁应用成功");
                }
                else
                {
                    Log.LogWarning("未找到 TMP_Text.set_text 方法");
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"TMP_Text.set_text 补丁失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 为 TMPro.TMP_Text 的 SetText 方法添加钩子（很多UI使用此方法而非属性设置器）
        /// </summary>
        private void PatchTMPTextSetTextMethod()
        {
            if (TMPTextType == null) return;

            try
            {
                var setTextMethod = TMPTextType.GetMethod("SetText", new[] { typeof(string) });
                if (setTextMethod != null)
                {
                    Log.LogInfo($"找到 TMP_Text.SetText 方法: {setTextMethod.DeclaringType?.Name}");
                    var prefix = new HarmonyMethod(typeof(TMPTextSetTextPatch), nameof(TMPTextSetTextPatch.Prefix));
                    HarmonyInstance.Patch(setTextMethod, prefix);
                    Log.LogInfo("TMP_Text.SetText 补丁应用成功");
                }
                else
                {
                    Log.LogInfo("未找到 TMP_Text.SetText(string) 方法");
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"TMP_Text.SetText 补丁失败: {ex.Message}");
            }
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
                        if (UnityObjectType == null)
                            UnityObjectType = assembly.GetType("UnityEngine.Object");
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

        /// <summary>
        /// 获取 GameObject 在场景中的完整路径
        /// </summary>
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

        /// <summary>
        /// 请求在主线程上执行扫描（由钩子触发）
        /// </summary>
        public static void RequestScan()
        {
            lock (_scanLock)
            {
                if ((DateTime.Now - _lastScanTime).TotalSeconds > 10)
                {
                    _scanRequested = true;
                }
            }
        }

        /// <summary>
        /// 在主线程上执行扫描（由钩子调用）
        /// </summary>
        public static void TryScanOnMainThread()
        {
            bool shouldScan = false;
            lock (_scanLock)
            {
                if (_scanRequested && (DateTime.Now - _lastScanTime).TotalSeconds > 10)
                {
                    shouldScan = true;
                    _scanRequested = false;
                    _lastScanTime = DateTime.Now;
                }
            }

            if (shouldScan)
            {
                ScanAllTexts();
            }
        }

        /// <summary>
        /// 扫描场景中所有文本组件，收集未翻译的文本
        /// </summary>
        public static void ScanAllTexts()
        {
            try
            {
                if (UnityObjectType == null)
                {
                    Log.LogWarning("无法扫描文本：未找到 UnityEngine.Object 类型");
                    return;
                }

                int count = 0;
                Type[] textTypes = new Type[] { TextType, TMPTextType };

                foreach (var textType in textTypes)
                {
                    if (textType == null)
                        continue;

                    try
                    {
                        Array objects = null;

                        var findGeneric = UnityObjectType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                            .FirstOrDefault(m => m.Name == "FindObjectsOfType" && m.IsGenericMethod);

                        if (findGeneric != null)
                        {
                            try
                            {
                                var specialized = findGeneric.MakeGenericMethod(textType);
                                var result = specialized.Invoke(null, null);
                                if (result != null)
                                {
                                    var lengthProp = result.GetType().GetProperty("Length");
                                    if (lengthProp != null)
                                    {
                                        var length = (int)lengthProp.GetValue(result);
                                        Log.LogInfo($"[泛型扫描] 找到 {length} 个 {textType.Name} 组件");

                                        objects = Array.CreateInstance(textType, length);
                                        var itemProp = result.GetType().GetProperty("Item", new[] { typeof(int) });
                                        for (int i = 0; i < length; i++)
                                        {
                                            var item = itemProp?.GetValue(result, new object[] { i });
                                            objects.SetValue(item, i);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.LogInfo($"泛型 FindObjectsOfType 失败: {ex.Message}");
                            }
                        }

                        if (objects == null)
                        {
                            var findNonGeneric = UnityObjectType.GetMethod("FindObjectsOfType", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Type) }, null);
                            if (findNonGeneric == null)
                            {
                                findNonGeneric = UnityObjectType.GetMethod("FindObjectsOfType", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Type), typeof(bool) }, null);
                            }

                            if (findNonGeneric != null)
                            {
                                try
                                {
                                    objects = findNonGeneric.Invoke(null, new object[] { textType }) as Array;
                                    Log.LogInfo($"[非泛型扫描] 找到 {objects?.Length ?? 0} 个 {textType.Name} 组件");
                                }
                                catch (Exception ex)
                                {
                                    Log.LogInfo($"非泛型 FindObjectsOfType 失败: {ex.Message}");
                                }
                            }
                        }

                        if (objects != null)
                        {
                            foreach (var obj in objects)
                            {
                                try
                                {
                                    var textProp = textType.GetProperty("text");
                                    var text = textProp?.GetValue(obj)?.ToString();
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        var gameObjectProp = textType.GetProperty("gameObject");
                                        var gameObject = gameObjectProp?.GetValue(obj);
                                        var path = gameObject != null ? GetGameObjectPath(gameObject) : "";

                                        if (ChatFilterMgr.ShouldTranslate(path, text))
                                        {
                                            TextCollectorMgr.CollectText(text);
                                            count++;
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.LogInfo($"扫描 {textType?.Name ?? "null"} 类型出错: {ex.Message}");
                    }
                }
                Log.LogInfo($"扫描完成，共收集 {count} 条新文本");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"扫描文本失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// UnityEngine.UI.Text 的 set_text 钩子
    /// </summary>
    public static class TextPatch
    {
        public static void Prefix(object __instance, ref string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            try
            {
                ChinesePatchPlugin.TryScanOnMainThread();

                var gameObjectProp = __instance.GetType().GetProperty("gameObject");
                var gameObject = gameObjectProp?.GetValue(__instance);
                string path = gameObject != null ? ChinesePatchPlugin.GetGameObjectPath(gameObject) : "";

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

    /// <summary>
    /// TMPro.TMP_Text 的 set_text 属性钩子
    /// </summary>
    public static class TMPTextPatch
    {
        public static void Prefix(object __instance, ref string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            try
            {
                ChinesePatchPlugin.TryScanOnMainThread();

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

    /// <summary>
    /// TMPro.TMP_Text 的 SetText 方法钩子（参数名为 sourceText）
    /// </summary>
    public static class TMPTextSetTextPatch
    {
        public static void Prefix(object __instance, ref string sourceText)
        {
            if (string.IsNullOrEmpty(sourceText))
                return;

            try
            {
                ChinesePatchPlugin.TryScanOnMainThread();

                if (ShouldTranslate(__instance, sourceText))
                {
                    sourceText = ChinesePatchPlugin.TranslationMgr.Translate(sourceText);
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
