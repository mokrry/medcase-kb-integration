using MedicalFeaturePrototype.Api.Models;

namespace MedicalFeaturePrototype.Api.Services.Interfaces;

public interface IFeatureAnalysisService
{
    IReadOnlyList<FeatureCheckResult> Analyze(string fullText, IReadOnlyList<TargetFeature> features);
}
