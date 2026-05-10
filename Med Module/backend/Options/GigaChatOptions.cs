namespace MedicalFeaturePrototype.Api.Options;

public class GigaChatOptions
{
    public string AuthorizationKey { get; set; } = string.Empty;
    public string Scope { get; set; } = "GIGACHAT_API_PERS";
    public string Model { get; set; } = "GigaChat";
    public string AuthBaseUrl { get; set; } = "https://ngw.devices.sberbank.ru:9443";
    public string ApiBaseUrl { get; set; } = "https://gigachat.devices.sberbank.ru/api";
}
