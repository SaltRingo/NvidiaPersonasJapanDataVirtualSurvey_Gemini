using System.Collections;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using NvidiaPersonasJapanDataVirtualSurvey.Core.Models;
using System.Text.RegularExpressions;

namespace NvidiaPersonasJapanDataVirtualSurvey.Core;

public class SurveyService
{
    private readonly GeminiChatService chatService;
    private readonly IProgress<string>? progress;

    private List<PersonaRecord> personaList;

    private SurveyService(string apiKey, string model, IProgress<string>? progress, List<PersonaRecord> personaList)
    {
        this.chatService = new GeminiChatService(apiKey, model, progress);
        this.progress = progress;
        this.personaList = personaList;
    }

    public static async Task<SurveyService> CreateAsync(string apiKey, string model, int SampleSize = 20, int? randomSeed = null, FilterOptions? filters = null, IProgress<string>? progress = null)
    {
        var loader = new DataLoader(progress);
        var personaList = await loader.LoadAsync(SampleSize, randomSeed, filters);
        
        return new SurveyService(apiKey, model, progress, personaList.ToList());
    }

    public async Task<SurveyResponse> RunSurveyAsync(ISurveyRequest request)
    {
        progress?.Report($"[{DateTime.Now:HH:mm:ss}] アンケート開始: {personaList.Count}人のペルソナに質問中...");

        SurveyResponse response = new SurveyResponse();

        for (int i = 0; i < personaList.Count; i++)
        {
            var persona = personaList[i];
            
            progress?.Report($"[{DateTime.Now:HH:mm:ss}] 進捗: {i + 1}/{personaList.Count} - {persona.Age}歳/{persona.Sex}/{persona.Occupation}");

            var systemPrompt = request.GetSystemPrompt(persona);
            var userPrompt = request.GetUserPrompt();

            var (rawResponse, inputTokens, outputTokens) = await chatService.GenerateResponseWithUsageAsync(
                systemPrompt,
                userPrompt,
                persona.Uuid.ToString()
            );

            // LLMの応答をまず正規化してからJSONとしてパースを試みる
            string parsedAnswer = string.Empty;
            string parsedReason = string.Empty;

            var normalized = NormalizeJsonCandidate(rawResponse);

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(normalized);
                var root = doc.RootElement;
                if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (root.TryGetProperty("answer", out var a)) parsedAnswer = a.GetString() ?? string.Empty;
                    if (root.TryGetProperty("reason", out var r)) parsedReason = r.GetString() ?? string.Empty;
                }
                else
                {
                    // 期待するJSONオブジェクトが来なかった場合はフォールバック
                    parsedAnswer = string.Empty;
                    parsedReason = rawResponse;
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // パース失敗時のフォールバック: answer を空にして reason に全文を入れる
                parsedAnswer = string.Empty;
                parsedReason = rawResponse;
            }

            response.Answers.Add(new PersonaAnswer(persona, parsedAnswer, parsedReason));
            response.Usage.PromptTokens += inputTokens;
            response.Usage.CompletionTokens += outputTokens;

            // APIレート制限対策のため、リクエスト間に遅延を入れる
            if (i < personaList.Count - 1)
            {
                // 16人以上の対象なら各リクエスト間に4秒以上待つ（クォータ回避）
                var delayMs = personaList.Count >= 16 ? 4000 : 100;
                await Task.Delay(delayMs);
            }
        }

        progress?.Report($"[{DateTime.Now:HH:mm:ss}] アンケート完了!");
        progress?.Report($"合計トークン使用量 - 入力: {response.Usage.PromptTokens}, 出力: {response.Usage.CompletionTokens}");

        return response;
    }

    private static string NormalizeJsonCandidate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;

        // 1) コードフェンスがあれば中身を取り出す（```json ... ``` または ``` ... ```）
        var fenced = Regex.Match(raw, "```(?:json)?\\s*([\\s\\S]*?)\\s*```", RegexOptions.IgnoreCase);
        if (fenced.Success)
        {
            raw = fenced.Groups[1].Value;
        }

        // 2) 最初の { から最後の } までを抽出
        var first = raw.IndexOf('{');
        var last = raw.LastIndexOf('}');
        if (first >= 0 && last > first)
        {
            raw = raw.Substring(first, last - first + 1);
        }

        // 3) 全角句点（、）がプロパティ間に使われている場合、半角カンマに置換する
        //    単純置換は文字列内も壊す恐れがあるため、ダブルクオートで囲まれた文字列内部の全角句点は置換しない。
        //    以下は単純な近似: ダブルクオートの間を避けて全角句点を置換する。
        var sb = new System.Text.StringBuilder();
        bool inString = false;
        for (int i = 0; i < raw.Length; i++)
        {
            var ch = raw[i];
            if (ch == '"')
            {
                sb.Append(ch);
                // 直前がバックスラッシュならエスケープされたダブルクオート
                bool escaped = i > 0 && raw[i - 1] == '\\';
                if (!escaped) inString = !inString;
                continue;
            }

            if (!inString && ch == '、')
            {
                sb.Append(',');
            }
            else
            {
                sb.Append(ch);
            }
        }

        return sb.ToString().Trim();
    }
}