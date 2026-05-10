namespace MedicalFeaturePrototype.Api.Dtos;

public class PromptSymptomVoteDto
{
    public string Symptom { get; set; } = string.Empty;
    public int Votes { get; set; }
    public List<string> Providers { get; set; } = [];
    public bool ReachedMajority { get; set; }
    public bool ResolvedByGemini { get; set; }
    public bool IncludedInFinalAnswer { get; set; }
}
