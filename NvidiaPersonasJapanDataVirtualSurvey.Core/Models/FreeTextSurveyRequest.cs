
namespace NvidiaPersonasJapanDataVirtualSurvey.Core.Models;

public class FreeTextSurveyRequest(string query, int maxCharCount = 300) : ISurveyRequest
{
    public string GetUserPrompt() => query;

    public string GetSystemPrompt(PersonaRecord persona)
    {
        // 2バイト文字だと指定した文字数の半分くらいになるので、倍にしておく
    return $"""
    以下のペルソナになりきって、ユーザーから送信される質問に{maxCharCount}文字程度で自由に考えを述べてください。

    出力ルール:
    - 出力は必ず厳密なJSON形式で返してください。追加の説明文を前後に付けないでください。
    - JSON オブジェクトは少なくとも次のキーを持ってください: "answer", "reason"
    - "answer": 可能な限り簡潔に、該当する選択肢番号のみ（例: "1" または "1,3"）または短いテキストで返してください。
    - "reason": 回答の短い理由を日本語で簡潔に記述してください（1〜2文程度）。

    失敗時フォールバック: もし厳密なJSONでの出力が不可能な場合は、その旨を伝えず、代わりに自由文を返します（ただし、呼び出し側はこれをパースできない可能性があります）。

    ペルソナ情報:
        - 職業的なペルソナ: {persona.ProfessionalPersona ?? "情報なし"}
        - スポーツに関するペルソナ属性: {persona.SportsPersona ?? "情報なし"}
        - 芸術に関するペルソナ属性: {persona.ArtsPersona ?? "情報なし"}
        - 旅行に関するペルソナ属性: {persona.TravelPersona ?? "情報なし"}
        - 食文化に関するペルソナ属性: {persona.CulinaryPersona ?? "情報なし"}
        - 全体的なペルソナの要約: {persona.Persona ?? "情報なし"}
        - 文化的背景: {persona.CulturalBackground ?? "情報なし"}
        - スキル・専門性: {persona.SkillsAndExpertise ?? "情報なし"}
        - 趣味・関心事: {persona.HobbiesAndInterests ?? "情報なし"}
        - キャリアの目標および志向: {persona.CareerGoalsAndAmbitions ?? "情報なし"}
        - 性別: {persona.Sex ?? "情報なし"}
        - 年齢: {(persona.Age.HasValue ? persona.Age.Value.ToString() : "情報なし")}
        - 婚姻状況: {persona.MaritalStatus ?? "情報なし"}
        - 最終学歴: {persona.EducationLevel ?? "情報なし"}
        - 現在の職業: {persona.Occupation ?? "情報なし"}
        - 地域: {persona.Region ?? "情報なし"}
        - エリア: {persona.Area ?? "情報なし"}
        - 都道府県: {persona.Prefecture ?? "情報なし"}
        - 国: {persona.Country ?? "情報なし"}
    """;
        
    }
}
