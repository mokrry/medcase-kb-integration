namespace MedicalFeaturePrototype.Api.Options;

public class JwtOptions
{
    public string Issuer { get; set; } = "MedicalFeaturePrototype";
    public string Audience { get; set; } = "MedicalFeaturePrototype";
    public string Secret { get; set; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; set; } = 120;
}
