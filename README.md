# GorgonChinesePatch 汉化插件

## 简介
Project: Gorgon 游戏中文汉化插件，自动收集游戏文本并使用 AI 翻译。

## 安装
1. 安装 BepInEx IL2CPP https://builds.bepinex.dev/projects/bepinex_be
2. 将编译后的 `GorgonChinesePatch.dll` 放入 `BepInEx/plugins/ChinesePatch/`
3. 启动游戏，插件会自动创建配置文件

## 配置说明

配置文件路径：`BepInEx/plugins/ChinesePatch/ai_config.json`

### 配置项说明

| 配置项 | 类型 | 说明 | 示例 |
|--------|------|------|------|
| `ai_platform` | string | AI 平台标识 | `qwen`、`deepseek`、`ollama` |
| `api_key` | string | API 密钥 | `sk-xxxxxxxxxxxx` |
| `api_url` | string | API 请求地址 | `https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions` |
| `model` | string | 使用的模型 | `qwen-plus` |
| `batch_size` | int | 每批翻译的文本数量 | `50` |
| `request_delay_ms` | int | 请求间隔时间（毫秒） | `1000` |
| `max_retries` | int | 失败重试次数 | `3` |
| `timeout_seconds` | int | 请求超时时间（秒） | `30` |
| `auto_translate` | bool | 是否启用自动翻译 | `true` 或 `false` |
| `translation_prompt` | string | AI 翻译提示词 | 见默认配置 |

### 常用 AI 平台配置

**通义千问（Qwen）**
```json
{
  "ai_platform": "qwen",
  "api_url": "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions",
  "model": "qwen-plus"
}
```

**DeepSeek**
```json
{
  "ai_platform": "deepseek",
  "api_url": "https://api.deepseek.com/anthropic",
  "model": "deepseek-v4-flash"
}
```

**Ollama（本地）**
```json
{
  "ai_platform": "ollama",
  "api_url": "http://localhost:11434/api/chat",
  "model": "qwen2.5:7b"
}
```

## 错误提示说明

| 错误信息 | 原因 | 解决方法 |
|----------|------|----------|
| `API余额不足，请充值` | API 账户余额不足 | 前往对应平台充值 |
| `API密钥无效，请检查配置` | API Key 错误或已过期 | 检查 `api_key` 配置是否正确 |
| `请求频率过高，请稍后重试` | 超过 API 频率限制 | 增大 `request_delay_ms` 值 |
| `AI网络请求错误` | 网络连接问题 | 检查网络，确认 API 地址可访问 |
| `AI请求超时` | 响应时间超过 `timeout_seconds` | 增大超时时间或检查网络 |

## 文件说明

| 文件 | 说明 |
|------|------|
| `ai_config.json` | AI 翻译配置 |
| `collected_texts.json` | 自动收集的游戏文本 |
| `translations.json` | 翻译结果（中文） |
