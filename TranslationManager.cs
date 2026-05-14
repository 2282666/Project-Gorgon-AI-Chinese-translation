using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;
using BepInEx.Logging;

namespace GorgonChinesePatch
{
    public class TranslationManager
    {
        private Dictionary<string, string> _translations;
        private string _translationsFilePath;
        private ManualLogSource _log;
        private TextCollector _textCollector;
        private AiTranslationManager _aiManager;
        private object _lock = new object();
        private bool _autoTranslateEnabled;
        private Task _translationTask;
        private bool _isTranslating;
        private System.Timers.Timer _checkTimer;

        public int TranslationCount => _translations.Count;

        public TranslationManager(string translationsPath, ManualLogSource log, TextCollector collector, AiTranslationManager aiManager)
        {
            _translationsFilePath = translationsPath;
            _log = log;
            _textCollector = collector;
            _aiManager = aiManager;
            _translations = new Dictionary<string, string>();
            LoadTranslations();
        }

        private void LoadTranslations()
        {
            try
            {
                if (File.Exists(_translationsFilePath))
                {
                    var json = File.ReadAllText(_translationsFilePath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dict != null)
                    {
                        _translations = dict;
                        _log.LogInfo($"加载了 {_translations.Count} 条翻译");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"加载翻译文件失败: {ex.Message}");
            }
        }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public void SaveTranslations()
        {
            try
            {
                lock (_lock)
                {
                    var json = JsonSerializer.Serialize(_translations, JsonOptions);
                    File.WriteAllText(_translationsFilePath, json);
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"保存翻译文件失败: {ex.Message}");
            }
        }

        public string Translate(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            lock (_lock)
            {
                if (_translations.TryGetValue(text, out var translation))
                    return translation;
            }

            _textCollector.CollectText(text);

            if (_autoTranslateEnabled && _aiManager.IsConfigured && !_isTranslating)
            {
                _isTranslating = true;
                _translationTask = Task.Run(async () =>
                {
                    await TranslateCollectedTexts();
                    _isTranslating = false;
                });
            }

            return text;
        }

        private async Task TranslateCollectedTexts()
        {
            try
            {
                int totalTranslated = 0;
                int batchCount = 0;

                while (true)
                {
                    var untranslated = _textCollector.GetUntranslatedTexts(50);
                    if (untranslated.Count == 0)
                    {
                        _log.LogInfo($"所有文本已翻译完成，共翻译 {totalTranslated} 条");
                        break;
                    }

                    batchCount++;
                    _log.LogInfo($"开始AI翻译批次 {batchCount}，共 {untranslated.Count} 条文本...");

                    var results = await _aiManager.TranslateBatchAsync(untranslated);

                    foreach (var kvp in results)
                    {
                        _textCollector.AddTranslation(kvp.Key, kvp.Value);
                        lock (_lock)
                        {
                            _translations[kvp.Key] = kvp.Value;
                        }
                        totalTranslated++;
                    }

                    SaveTranslations();
                    _log.LogInfo($"批次 {batchCount} 完成，已翻译 {totalTranslated} 条");
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"AI翻译过程出错: {ex.Message}");
            }
        }

        public void EnableAutoTranslate()
        {
            _autoTranslateEnabled = true;
            _log.LogInfo("自动翻译已启用");

            _checkTimer = new System.Timers.Timer(3000);
            _checkTimer.Elapsed += async (s, e) => await CheckAndTranslate();
            _checkTimer.Start();
            _log.LogInfo("定期检查已启动，每3秒检查一次新文本");
        }

        private async Task CheckAndTranslate()
        {
            if (_autoTranslateEnabled && _aiManager.IsConfigured && !_isTranslating)
            {
                var untranslated = _textCollector.GetUntranslatedTexts(50);
                if (untranslated.Count > 0)
                {
                    _isTranslating = true;
                    try
                    {
                        _log.LogInfo($"检测到 {untranslated.Count} 条新文本，开始翻译...");
                        var results = await _aiManager.TranslateBatchAsync(untranslated);

                        foreach (var kvp in results)
                        {
                            _textCollector.AddTranslation(kvp.Key, kvp.Value);
                            lock (_lock)
                            {
                                _translations[kvp.Key] = kvp.Value;
                            }
                        }

                        SaveTranslations();
                        _log.LogInfo($"定期检查翻译完成，已翻译 {results.Count} 条");
                    }
                    catch (Exception ex)
                    {
                        _log.LogError($"定期检查翻译出错: {ex.Message}");
                    }
                    finally
                    {
                        _isTranslating = false;
                    }
                }
            }
        }

        public void DisableAutoTranslate()
        {
            _autoTranslateEnabled = false;
            _log.LogInfo("自动翻译已禁用");
        }

        public string GetStats()
        {
            return $"翻译数: {TranslationCount}, 收集文本: {_textCollector.CollectedCount}";
        }
    }
}
