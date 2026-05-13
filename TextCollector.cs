using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using BepInEx.Logging;

namespace GorgonChinesePatch
{
    public class TextCollector
    {
        private HashSet<string> _collectedTexts;
        private Dictionary<string, string> _translations;
        private string _textsFilePath;
        private string _translationsFilePath;
        private ManualLogSource _log;
        private object _lock = new object();

        public int CollectedCount => _collectedTexts.Count;
        public int TranslatedCount => _translations.Count;

        public TextCollector(string textsPath, string translationsPath, ManualLogSource log)
        {
            _textsFilePath = textsPath;
            _translationsFilePath = translationsPath;
            _log = log;
            _collectedTexts = new HashSet<string>();
            _translations = new Dictionary<string, string>();
            LoadCollectedTexts();
            LoadTranslations();
        }

        private void LoadCollectedTexts()
        {
            try
            {
                if (File.Exists(_textsFilePath))
                {
                    var json = File.ReadAllText(_textsFilePath);
                    var list = JsonSerializer.Deserialize<List<string>>(json);
                    if (list != null)
                        _collectedTexts = new HashSet<string>(list);
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"加载已收集文本失败: {ex.Message}");
            }
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
                        _translations = dict;
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"加载翻译失败: {ex.Message}");
            }
        }

        public void CollectText(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length < 2)
                return;

            lock (_lock)
            {
                if (!_collectedTexts.Contains(text))
                {
                    _collectedTexts.Add(text);
                    SaveCollectedTexts();
                }
            }
        }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private void SaveCollectedTexts()
        {
            try
            {
                var json = JsonSerializer.Serialize(_collectedTexts.ToList(), JsonOptions);
                File.WriteAllText(_textsFilePath, json);
            }
            catch (Exception ex)
            {
                _log.LogWarning($"保存已收集文本失败: {ex.Message}");
            }
        }

        public void SaveTranslations()
        {
            try
            {
                var json = JsonSerializer.Serialize(_translations, JsonOptions);
                File.WriteAllText(_translationsFilePath, json);
            }
            catch (Exception ex)
            {
                _log.LogWarning($"保存翻译失败: {ex.Message}");
            }
        }

        public string GetTranslation(string text)
        {
            if (_translations.TryGetValue(text, out var translation))
                return translation;
            return null;
        }

        public void AddTranslation(string original, string translated)
        {
            lock (_lock)
            {
                _translations[original] = translated;
                SaveTranslations();
            }
        }

        public List<string> GetUntranslatedTexts(int maxCount = 50)
        {
            lock (_lock)
            {
                return _collectedTexts
                    .Where(t => !_translations.ContainsKey(t))
                    .Take(maxCount)
                    .ToList();
            }
        }

        public void AddTranslations(Dictionary<string, string> newTranslations)
        {
            lock (_lock)
            {
                foreach (var kvp in newTranslations)
                    _translations[kvp.Key] = kvp.Value;
                SaveTranslations();
            }
        }
    }
}
