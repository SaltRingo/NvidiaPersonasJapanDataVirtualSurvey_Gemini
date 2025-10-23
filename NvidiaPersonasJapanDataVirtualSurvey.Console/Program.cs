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

// コマンドライン引数の簡易パース
string? prefectureArg = null;
string? educationArg = null;
string? sexArg = null;
int? cliSampleSize = null;

for (int i = 0; i < args.Length; i++)
{
    var a = args[i];
    if ((a == "--prefecture" || a == "-p") && i + 1 < args.Length)
    {
        prefectureArg = args[i + 1];
        i++;
    }
    else if ((a == "--education" || a == "-e") && i + 1 < args.Length)
    {
        educationArg = args[i + 1];
        i++;
    }
    else if ((a == "--sex" || a == "-s") && i + 1 < args.Length)
    {
        sexArg = args[i + 1];
        i++;
    }
    else if ((a == "--sample-size" || a == "-n") && i + 1 < args.Length && int.TryParse(args[i + 1], out var n))
    {
        cliSampleSize = n;
        i++;
    }
}

// フィルタが指定されていればそれを優先、なければ sampleSize を使ってランダム抽出
var filters = (prefectureArg, educationArg, sexArg) switch
{
    (null, null, null) => null,
    _ => new FilterOptions(prefectureArg, educationArg, sexArg)
};

var effectiveSampleSize = cliSampleSize ?? sampleSize;

var service = await SurveyService.CreateAsync(apiKey, model, effectiveSampleSize, randomSeed, filters, new Progress<string>(OnProgressChanged));

// 設定値取得のヘルパー関数
string? GetConfigValue(string key)
{
    return configuration[key] // User Secrets & 環境変数
           ?? Environment.GetEnvironmentVariable(key); // .envファイル経由
}

// ターミナルから自由入力で質問を受け取り、その質問をフィルタ対象のペルソナ全員（またはランダム抽出）に対して実行する
Console.WriteLine("質問を入力してください（1 行で Enter）：");
var userQuestion = Console.ReadLine();
if (string.IsNullOrWhiteSpace(userQuestion))
{
    Console.WriteLine("質問が入力されませんでした。処理を終了します。");
    return;
}

var surveys = new List<ISurveyRequest>
{
    new FreeTextSurveyRequest(userQuestion.Trim(), 300)
};

SurveyResponse? finalResult = null;

for (int i = 0; i < surveys.Count; i++)
{
    Console.WriteLine($"\n📋 アンケート {i + 1}/{surveys.Count} を実行中...");
    Console.WriteLine($"質問: {GetQuestionText(surveys[i])}");
    Console.WriteLine(new string('=', 50));

    var result = await service.RunSurveyAsync(surveys[i]);
    finalResult = result;

    Console.WriteLine("=== Survey Results ===");
    Console.WriteLine($"Total Prompt Tokens: {result.Usage.PromptTokens}");
    Console.WriteLine($"Total Completion Tokens: {result.Usage.CompletionTokens}");
    Console.WriteLine($"Total Tokens: {result.Usage.TotalTokens}");
    Console.WriteLine();

    foreach (var answer in result.Answers)
    {
        DisplayPersonaDetails(answer.Persona);
        Console.WriteLine($"🗣️  **回答**: {answer.Answer}");
        Console.WriteLine(new string('=', 80));
    }
    Console.WriteLine("========================");

}

if (finalResult is not null)
{
    // CSV に結果を保存（UTF-8 with BOM）
try
{
    var outFile = Path.Combine(Directory.GetCurrentDirectory(), "survey_results.csv");
        using var fs = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.None);
        using var sw = new StreamWriter(fs, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        // ヘッダー
        sw.WriteLine("Occupation,Answer,Reason");

        foreach (var ans in finalResult.Answers)
        {
            var occupation = ans.Persona.Occupation?.Replace(',', '、').Replace("\n", " ") ?? string.Empty;
            var a = ans.Answer?.Replace(',', '、').Replace("\n", " ") ?? string.Empty;
            var r = ans.Reason?.Replace(',', '、').Replace("\n", " ") ?? string.Empty;
            sw.WriteLine($"{occupation},{a},{r}");
        }

        sw.Flush();
        Console.WriteLine($"保存しました: {outFile}");
}
catch (Exception ex)
{
    Console.WriteLine($"CSV 書き出し時にエラー: {ex.Message}");
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

// ペルソナ詳細情報を表示するヘルパー関数
void DisplayPersonaDetails(PersonaRecord persona)
{
    Console.WriteLine($"👤 **ペルソナ詳細**");
    Console.WriteLine($"   📊 基本情報: {persona.Age}歳 / {persona.Sex} / {persona.Prefecture} 在住");
    Console.WriteLine($"   💼 職業: {persona.Occupation}");
    
    if (!string.IsNullOrEmpty(persona.MaritalStatus))
        Console.WriteLine($"   👨‍👩‍👧‍👦 家族構成: {persona.MaritalStatus}");
    
    if (!string.IsNullOrEmpty(persona.EducationLevel))
        Console.WriteLine($"   🎓 学歴: {persona.EducationLevel}");
    
    if (!string.IsNullOrEmpty(persona.CulturalBackground))
    {
        Console.WriteLine($"   🌏 文化的背景: {TruncateText(persona.CulturalBackground, 100)}");
    }
    
    if (!string.IsNullOrEmpty(persona.HobbiesAndInterests))
    {
        Console.WriteLine($"   🎯 趣味・興味: {TruncateText(persona.HobbiesAndInterests, 100)}");
    }
    
    if (!string.IsNullOrEmpty(persona.SkillsAndExpertise))
    {
        Console.WriteLine($"   💡 スキル・専門性: {TruncateText(persona.SkillsAndExpertise, 100)}");
    }
    
    if (!string.IsNullOrEmpty(persona.CareerGoalsAndAmbitions))
    {
        Console.WriteLine($"   🚀 キャリア目標: {TruncateText(persona.CareerGoalsAndAmbitions, 100)}");
    }
    
    Console.WriteLine($"   🆔 UUID: {persona.Uuid}");
    Console.WriteLine();
}

// テキストを指定した長さで切り詰める関数
string TruncateText(string text, int maxLength)
{
    if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        return text;
    
    return text.Substring(0, maxLength) + "...";
}
Console.ReadLine();