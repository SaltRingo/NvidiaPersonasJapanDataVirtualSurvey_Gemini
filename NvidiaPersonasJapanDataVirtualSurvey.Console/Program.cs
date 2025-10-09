using NvidiaPersonasJapanDataVirtualSurvey.Core;
using NvidiaPersonasJapanDataVirtualSurvey.Core.Models;
using Microsoft.Extensions.Configuration;
using DotNetEnv;

// まず.envファイルを読み込み
var envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envFile))
{
    Env.Load(envFile);
    Console.WriteLine($"✅ .envファイルを読み込みました: {envFile}");
}
else
{
    // 親ディレクトリもチェック
    var parentEnvFile = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory())!.FullName, ".env");
    if (File.Exists(parentEnvFile))
    {
        Env.Load(parentEnvFile);
        Console.WriteLine($"✅ .envファイルを読み込みました: {parentEnvFile}");
    }
    else
    {
        Console.WriteLine($"⚠️  .envファイルが見つかりません: {envFile}");
    }
}

// 設定読み込み（複数ソース対応）
var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>() // dotnet user-secrets
    .AddEnvironmentVariables() // 環境変数
    .Build();

void OnProgressChanged(string message)
{
    Console.WriteLine(message);
}

// 設定値を取得（複数のソースから優先順位付きで取得）
var apiKey = GetConfigValue("GEMINI_API_KEY") 
    ?? throw new InvalidOperationException("GEMINI_API_KEY が設定されていません。README.md の設定方法を参照してください。");

var model = GetConfigValue("GEMINI_MODEL") ?? "gemini-2.5-flash";
var sampleSize = int.Parse(GetConfigValue("SAMPLE_SIZE") ?? "5");

// シード値の取得（オプション）
int? randomSeed = null;
var seedValue = GetConfigValue("RANDOM_SEED");
if (!string.IsNullOrWhiteSpace(seedValue) && int.TryParse(seedValue, out var seed))
{
    randomSeed = seed;
}

Console.WriteLine($"使用モデル: {model}");
Console.WriteLine($"サンプル数: {sampleSize}");
if (randomSeed.HasValue)
{
    Console.WriteLine($"🔒 固定ペルソナ選択 (シード値: {randomSeed.Value})");
}
else
{
    Console.WriteLine($"🎲 ランダムペルソナ選択");
}
Console.WriteLine();

var service = await SurveyService.CreateAsync(apiKey, model, sampleSize, randomSeed, new Progress<string>(OnProgressChanged));

// 設定値取得のヘルパー関数
string? GetConfigValue(string key)
{
    return configuration[key] // User Secrets & 環境変数
           ?? Environment.GetEnvironmentVariable(key); // .envファイル経由
}

// 複数のアンケートを順次実行する例
var surveys = new List<ISurveyRequest>
{
    new FreeTextSurveyRequest("昨今のAI(LLM)の目覚ましい進化についてどう思いますか？", 300),
    new YesNoSurveyRequest("現在の日本において金融緩和政策は必要だと思いますか？"),
    new OptionSelectSurveyRequest(
        "あなたが食べて見たいのはどちらですか？",
        new List<string> { "正統派芋煮", "庄内風芋煮" },
        isMultiSelect: false
    )
};

for (int i = 0; i < surveys.Count; i++)
{
    Console.WriteLine($"\n📋 アンケート {i + 1}/{surveys.Count} を実行中...");
    Console.WriteLine($"質問: {GetQuestionText(surveys[i])}");
    Console.WriteLine(new string('=', 50));
    
    var result = await service.RunSurveyAsync(surveys[i]);
    
    Console.WriteLine("=== Survey Results ===");
    Console.WriteLine($"Total Prompt Tokens: {result.Usage.PromptTokens}");
    Console.WriteLine($"Total Completion Tokens: {result.Usage.CompletionTokens}");
    Console.WriteLine($"Total Tokens: {result.Usage.TotalTokens}");
    Console.WriteLine();
    
    foreach (var answer in result.Answers)
    {
        Console.WriteLine($"{answer.Persona.Age}歳 / {answer.Persona.Sex} / {answer.Persona.Occupation} / {answer.Persona.Prefecture} 在住");
        Console.WriteLine($"Answer: {answer.Answer}");
        Console.WriteLine("----------------------");
    }
    Console.WriteLine("========================");
    
    if (i < surveys.Count - 1)
    {
        Console.WriteLine("\n⏸️  次のアンケートまで3秒待機...");
        await Task.Delay(3000);
    }
}

// 質問文を取得するヘルパー関数
string GetQuestionText(ISurveyRequest survey)
{
    return survey switch
    {
        FreeTextSurveyRequest ftr => ftr.GetUserPrompt().Split('\n')[0],
        YesNoSurveyRequest ynr => ynr.GetUserPrompt().Split('\n')[0],
        OptionSelectSurveyRequest osr => osr.GetUserPrompt().Split('\n')[0],
        _ => "質問"
    };
}
Console.ReadLine();