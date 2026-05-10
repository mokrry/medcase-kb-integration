namespace MedicalFeaturePrototype.Api.Services.Interfaces;

public interface IGigaChatTokenService
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
