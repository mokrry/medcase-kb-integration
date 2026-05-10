namespace MedicalFeaturePrototype.Api.Dtos;

public class PromptVotingSummaryDto
{
    public int RequestedProviders { get; set; }
    public int SuccessfulProviders { get; set; }
    public int MajorityThreshold { get; set; }
    public string PreparedComplaintsText { get; set; } = string.Empty;
    public List<string> ConsensusSymptoms { get; set; } = [];
    public List<string> DisputedSymptoms { get; set; } = [];
    public List<string> GeminiConfirmedSymptoms { get; set; } = [];
    public List<string> FinalSymptoms { get; set; } = [];
    public List<PromptSymptomVoteDto> VoteDetails { get; set; } = [];
}
