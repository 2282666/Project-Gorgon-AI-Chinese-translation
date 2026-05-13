using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BepInEx.Logging;

namespace GorgonChinesePatch
{
    public class AiConfig
    {
        public string ai_platform { get; set; } = "qwen";
        public string api_key { get; set; } = "";
        public string api_url { get; set; } = "";
        public string model { get; set; } = "";
        public int batch_size { get; set; } = 50;
        public int request_delay_ms { get; set; } = 1000;
        public int max_retries { get; set; } = 3;
        public int timeout_seconds { get; set; } = 30;
        public bool auto_translate { get; set; } = false;
        public string translation_prompt { get; set; } = "";
    }

    public class AiTranslationManager
    {
        private static AiTranslationManager _instance;
        public static AiTranslationManager Instance => _instance ??= new AiTranslationManager();

        private AiConfig _config;
        private HttpClient _httpClient;
        private ManualLogSource _log;
        private string _configPath;

        public bool IsConfigured => !string.IsNullOrEmpty(_config?.api_key);
        public bool AutoTranslate => _config?.auto_translate ?? false;
        public int BatchSize => _config?.batch_size ?? 50;

        public void Initialize(string configPath, ManualLogSource log)
        {
            _configPath = configPath;
            _log = log;
            LoadConfig();
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(_config.timeout_seconds)
            };
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    _config = JsonSerializer.Deserialize<AiConfig>(json);
                    _log.LogInfo($"AI配置加载成功: {_config.ai_platform}, URL: {_config.api_url}, Model: {_config.model}");
                }
                else
                {
                    _config = new AiConfig();
                    SaveConfig();
                    _log.LogWarning($"AI配置文件不存在，已创建默认配置: {_configPath}");
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"加载AI配置失败: {ex.Message}");
                _config = new AiConfig();
            }
        }

        public void SaveConfig()
        {
            try
            {
                var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                _log.LogError($"保存AI配置失败: {ex.Message}");
            }
        }

        public async Task<string> TranslateTextAsync(string text)
        {
            if (string.IsNullOrEmpty(text) || !IsConfigured)
                return text;

            try
            {
                var messages = new[]
                {
                    new { role = "system", content = _config.translation_prompt },
                    new { role = "user", content = text }
                };

                var requestBody = new
                {
                    model = _config.model,
                    messages = messages,
                    temperature = 0.3,
                    max_tokens = 2000
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.api_key}");

                for (int i = 0; i < _config.max_retries; i++)
                {
                    try
                    {
                        var response = await _httpClient.PostAsync(_config.api_url, content);
                        var responseJson = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            _log.LogDebug($"AI响应: {responseJson.Substring(0, Math.Min(200, responseJson.Length))}");
                            
                            using var doc = JsonDocument.Parse(responseJson);
                            var choices = doc.RootElement.GetProperty("choices");
                            if (choices.GetArrayLength() > 0)
                            {
                                var translated = choices[0].GetProperty("message").GetProperty("content").GetString();
                                _log.LogInfo($"AI翻译成功: 原文长度={text.Length}, 译文长度={translated?.Length ?? 0}");
                                return translated?.Trim() ?? text;
                            }
                        }
                        else
                        {
                            var errorMsg = ParseApiError(responseJson);
                            _log.LogError($"AI翻译失败 (HTTP {response.StatusCode}): {errorMsg}");
                            return text;
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        _log.LogError($"AI网络请求错误: {ex.Message}");
                        return text;
                    }
                    catch (TaskCanceledException ex)
                    {
                        _log.LogError($"AI请求超时: {ex.Message}");
                        return text;
                    }
                    catch (Exception ex)
                    {
                        _log.LogError($"AI翻译异常: {ex.Message}");
                        return text;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"AI翻译失败: {ex.Message}");
            }

            return text;
        }

        private string ParseApiError(string responseJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var errorObj))
                {
                    var message = errorObj.GetProperty("message").GetString() ?? "";
                    var code = errorObj.GetProperty("code").GetString() ?? "";
                    var type = errorObj.GetProperty("type").GetString() ?? "";

                    if (type.Contains("insufficient") || code.Contains("quota") || message.Contains("quota"))
                        return $"API余额不足，请充值。详细信息: {message}";
                    if (type.Contains("invalid") && message.Contains("key"))
                        return $"API密钥无效，请检查配置。详细信息: {message}";
                    if (type.Contains("rate"))
                        return $"请求频率过高，请稍后重试。详细信息: {message}";

                    return $"{type}: {message}";
                }

                return responseJson.Length > 200 ? responseJson.Substring(0, 200) : responseJson;
            }
            catch
            {
                return responseJson.Length > 200 ? responseJson.Substring(0, 200) : responseJson;
            }
        }

        public async Task<Dictionary<string, string>> TranslateBatchAsync(List<string> texts)
        {
            var result = new Dictionary<string, string>();
            var batchSize = _config.batch_size;

            _log.LogInfo($"开始批量翻译，共 {texts.Count} 条文本，批次大小: {batchSize}");

            for (int i = 0; i < texts.Count; i += batchSize)
            {
                var batch = texts.GetRange(i, Math.Min(batchSize, texts.Count - i));
                _log.LogInfo($"翻译批次 {i / batchSize + 1}，包含 {batch.Count} 条文本");

                var numberedTexts = new StringBuilder();
                for (int j = 0; j < batch.Count; j++)
                {
                    numberedTexts.AppendLine($"[{j + 1}] {batch[j]}");
                }

                var prompt = $"请翻译以下文本，按编号返回翻译结果，每行一个翻译结果，格式为：[编号] 翻译结果\n\n{numberedTexts}";

                var translated = await TranslateTextAsync(prompt);

                var lines = translated.Split('\n');
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("[") && trimmedLine.Contains("]"))
                    {
                        var bracketEnd = trimmedLine.IndexOf(']');
                        var numberStr = trimmedLine.Substring(1, bracketEnd - 1);
                        if (int.TryParse(numberStr, out var number) && number >= 1 && number <= batch.Count)
                        {
                            var translatedText = trimmedLine.Substring(bracketEnd + 1).Trim();
                            if (!string.IsNullOrEmpty(translatedText))
                            {
                                result[batch[number - 1]] = translatedText;
                            }
                        }
                    }
                }

                _log.LogInfo($"批次 {i / batchSize + 1} 翻译完成，成功翻译 {result.Count}/{batch.Count} 条");

                if (i + batchSize < texts.Count)
                    await Task.Delay(_config.request_delay_ms);
            }

            _log.LogInfo($"批量翻译完成，共翻译 {result.Count}/{texts.Count} 条文本");
            return result;
        }
    }
}
