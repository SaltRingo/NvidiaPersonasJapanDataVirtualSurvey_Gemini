using System.Collections;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using NvidiaPersonasJapanDataVirtualSurvey.Core.Models;

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

            var (answer, inputTokens, outputTokens) = await chatService.GenerateResponseWithUsageAsync(
                systemPrompt, 
                userPrompt, 
                persona.Uuid.ToString()
            );

            response.Answers.Add(new PersonaAnswer(persona, answer));
            response.Usage.PromptTokens += inputTokens;
            response.Usage.CompletionTokens += outputTokens;

            // APIレート制限対策のため、リクエスト間に小さな遅延を入れる
            if (i < personaList.Count - 1)
            {
                await Task.Delay(500); // 500ms待機
            }
        }

        progress?.Report($"[{DateTime.Now:HH:mm:ss}] アンケート完了!");
        progress?.Report($"合計トークン使用量 - 入力: {response.Usage.PromptTokens}, 出力: {response.Usage.CompletionTokens}");

        return response;
    }
}