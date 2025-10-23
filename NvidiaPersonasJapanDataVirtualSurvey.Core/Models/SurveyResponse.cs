using System.Text.Json.Serialization;

namespace NvidiaPersonasJapanDataVirtualSurvey.Core.Models;

public class SurveyResponse
{
    public List<PersonaAnswer> Answers { get; set; } = new List<PersonaAnswer>();
    public UsageToken Usage { get; set; } = new UsageToken();
}

public class PersonaAnswer
{
    public PersonaRecord Persona { get; set; }
    public string Answer { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;

    public PersonaAnswer(PersonaRecord persona, string answer, string reason)
    {
        Persona = persona;
        Answer = answer;
        Reason = reason;
    }
}

public class UsageToken
{
    public int CompletionTokens { get; set; } = 0;
    public int PromptTokens { get; set; } = 0;
    public int TotalTokens => CompletionTokens + PromptTokens;
}