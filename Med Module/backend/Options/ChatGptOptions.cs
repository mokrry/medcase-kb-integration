namespace MedicalFeaturePrototype.Api.Options;

public class ChatGptOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-5.4-mini";
    public string BaseUrl { get; set; } = "https://api.proxyapi.ru/openai";
}
