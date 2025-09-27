using System.Text.Json.Serialization;

namespace AvaloniaXplat.Models
{
public class SamsungAuth
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; set; }
    [JsonPropertyName("token_type")]
    public required string TokenType { get; set; }
    [JsonPropertyName("access_token_expires_in")]
    public string? AccessTokenExpiresIn { get; set; }
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }
    [JsonPropertyName("refresh_token_expires_in")]
    public string? RefreshTokenExpiresIn { get; set; }
    [JsonPropertyName("userId")]
    public required string UserId { get; set; }
    [JsonPropertyName("client_id")]
    public string? ClientId { get; set; }
    [JsonPropertyName("inputEmailID")]
    public string? InputEmailID { get; set; }
    [JsonPropertyName("api_server_url")]
    public string? ApiServerUrl { get; set; }
    [JsonPropertyName("auth_server_url")]
    public string? AuthServerUrl { get; set; }
    [JsonPropertyName("close")]
    public bool Close { get; set; }
    [JsonPropertyName("closedAction")]
    public string? ClosedAction { get; set; }
    [JsonPropertyName("state")]
    public string? State { get; set; }
}
}
