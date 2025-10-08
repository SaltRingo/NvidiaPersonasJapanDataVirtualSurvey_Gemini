using System.Text.Json;
using System.Net.Http.Json;
using NvidiaPersonasJapanDataVirtualSurvey.Core.Models;

namespace NvidiaPersonasJapanDataVirtualSurvey.Core;

internal class GeminiChatService : IDisposable
{
    private readonly HttpClient httpClient;
    private readonly string model;
    private readonly string apiKey;
    private readonly IProgress<string>? progress;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    internal GeminiChatService(string apiKey, string model, IProgress<string>? progress = null)
    {
        this.httpClient = new HttpClient();
        this.model = model;
        this.apiKey = apiKey;
        this.progress = progress;
    }

    internal async Task<string> GenerateResponseAsync(string systemPrompt, string userPrompt, string personaId)
    {
        try
        {
            progress?.Report($"[{DateTime.Now:HH:mm:ss}] ペルソナ {personaId} の回答を生成中...");

            // Gemini API用のリクエストデータを構築
            var requestData = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = $"{systemPrompt}\n\n{userPrompt}" }
                        }
                    }
                }
            };

            var url = $"{BaseUrl}/{model}:generateContent?key={apiKey}";
            var response = await httpClient.PostAsJsonAsync(url, requestData);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API Error: {response.StatusCode} - {errorContent}");
            }

            var jsonResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
            var content = jsonResponse
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "";
            
            progress?.Report($"[{DateTime.Now:HH:mm:ss}] ペルソナ {personaId} の回答を取得完了");
            
            return content;
        }
        catch (Exception ex)
        {
            progress?.Report($"[{DateTime.Now:HH:mm:ss}] エラー - ペルソナ {personaId}: {ex.Message}");
            return $"回答の生成中にエラーが発生しました: {ex.Message}";
        }
    }

    internal async Task<(string response, int inputTokens, int outputTokens)> GenerateResponseWithUsageAsync(string systemPrompt, string userPrompt, string personaId)
    {
        try
        {
            progress?.Report($"[{DateTime.Now:HH:mm:ss}] ペルソナ {personaId} の回答を生成中...");

            var content = await GenerateResponseAsync(systemPrompt, userPrompt, personaId);
            
            // Gemini APIではトークン使用量の詳細取得が制限されているため、概算値を使用
            var inputPrompt = $"{systemPrompt}\n\n{userPrompt}";
            var inputTokens = EstimateTokens(inputPrompt);
            var outputTokens = EstimateTokens(content);
            
            progress?.Report($"[{DateTime.Now:HH:mm:ss}] ペルソナ {personaId} の回答を取得完了 (推定: 入力{inputTokens}トークン、出力{outputTokens}トークン)");
            
            return (content, inputTokens, outputTokens);
        }
        catch (Exception ex)
        {
            progress?.Report($"[{DateTime.Now:HH:mm:ss}] エラー - ペルソナ {personaId}: {ex.Message}");
            return ($"回答の生成中にエラーが発生しました: {ex.Message}", 0, 0);
        }
    }

    private int EstimateTokens(string text)
    {
        // 日本語と英語混在のテキストに対する概算
        // 1トークン ≈ 0.75単語 (英語) / 1文字 (日本語) として計算
        if (string.IsNullOrEmpty(text)) return 0;
        
        // 簡易的な推定: 文字数 / 2 （日本語の場合、平均的に2文字で1トークン程度）
        return Math.Max(1, text.Length / 2);
    }

    public void Dispose()
    {
        httpClient?.Dispose();
    }
}