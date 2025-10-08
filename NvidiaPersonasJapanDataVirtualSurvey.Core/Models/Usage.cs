namespace NvidiaPersonasJapanDataVirtualSurvey.Core.Models;

public record Usage
{
    public int CompletionTokens { get; set; }

    public int PromptTokens { get; set; }

    public int TotalTokens => PromptTokens + CompletionTokens;
}
