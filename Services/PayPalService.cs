using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TripWings.Services;

public class PayPalService : IPayPalService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PayPalService> _logger;
    private readonly HttpClient _httpClient;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public PayPalService(IConfiguration configuration, ILogger<PayPalService> logger, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        
        var baseUrl = _configuration["PayPalSettings:BaseUrl"] ?? "https://api.sandbox.paypal.com";
        _httpClient.BaseAddress = new Uri(baseUrl);
    }

    public async Task<string?> GetAccessTokenAsync()
    {

        if (_accessToken != null && _tokenExpiry > DateTime.UtcNow.AddMinutes(5))
        {
            return _accessToken;
        }

        try
        {
            var clientId = _configuration["PayPalSettings:ClientId"];
            var secretKey = _configuration["PayPalSettings:SecretKey"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(secretKey))
            {
                _logger.LogError("PayPal credentials are not configured");
                return null;
            }

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secretKey}"));
            
            var request = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
            request.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            });

            var response = await _httpClient.SendAsync(request);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<JsonElement>(content);
                
                if (tokenResponse.TryGetProperty("access_token", out var token))
                {
                    _accessToken = token.GetString();

                    var expiresIn = tokenResponse.TryGetProperty("expires_in", out var expires) 
                        ? expires.GetInt32() 
                        : 32400;
                    _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 300); // 5 minute buffer
                    
                    _logger.LogInformation("PayPal access token obtained successfully");
                    return _accessToken;
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to get PayPal access token: {response.StatusCode} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting PayPal access token");
        }

        return null;
    }

    public async Task<(bool Success, string? PaymentId, string? ApprovalUrl, string? ErrorMessage)> CreatePaymentAsync(
        decimal amount, 
        string currency, 
        string description,
        string returnUrl,
        string cancelUrl)
    {
        try
        {
            var accessToken = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                return (false, null, null, "לא ניתן להתחבר ל-PayPal. אנא נסה שוב מאוחר יותר.");
            }

            var paymentRequest = new
            {
                intent = "sale",
                payer = new { payment_method = "paypal" },
                transactions = new[]
                {
                    new
                    {
                        amount = new
                        {
                            total = amount.ToString("F2"),
                            currency = currency
                        },
                        description = description
                    }
                },
                redirect_urls = new
                {
                    return_url = returnUrl,
                    cancel_url = cancelUrl
                }
            };

            var json = JsonSerializer.Serialize(paymentRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "/v1/payments/payment");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var paymentResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (paymentResponse.TryGetProperty("id", out var paymentId))
                {
                    var id = paymentId.GetString();

                    string? approvalUrl = null;
                    if (paymentResponse.TryGetProperty("links", out var links) && links.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var link in links.EnumerateArray())
                        {
                            if (link.TryGetProperty("rel", out var rel) && 
                                rel.GetString() == "approval_url" &&
                                link.TryGetProperty("href", out var href))
                            {
                                approvalUrl = href.GetString();
                                break;
                            }
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(approvalUrl))
                    {
                        _logger.LogInformation($"PayPal payment created: {id}. Approval URL: {approvalUrl}");
                        return (true, id, approvalUrl, null);
                    }
                    else
                    {
                        _logger.LogWarning($"PayPal payment created but approval URL not found: {id}");
                        return (false, id, null, "לא ניתן למצוא כתובת אישור PayPal.");
                    }
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to create PayPal payment: {response.StatusCode} - {errorContent}");
                return (false, null, null, "שגיאה ביצירת תשלום PayPal. אנא נסה שוב.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating PayPal payment");
            return (false, null, null, "אירעה שגיאה בעת יצירת תשלום PayPal.");
        }

        return (false, null, null, "לא ניתן ליצור תשלום PayPal.");
    }

    public async Task<(bool Success, string? ErrorMessage)> ExecutePaymentAsync(string paymentId, string payerId)
    {
        try
        {
            var accessToken = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                return (false, "לא ניתן להתחבר ל-PayPal.");
            }

            var executeRequest = new
            {
                payer_id = payerId
            };

            var json = JsonSerializer.Serialize(executeRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/payments/payment/{paymentId}/execute");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var executeResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (executeResponse.TryGetProperty("state", out var state))
                {
                    var stateValue = state.GetString();
                    if (stateValue == "approved")
                    {
                        _logger.LogInformation($"PayPal payment executed successfully: {paymentId}");
                        return (true, null);
                    }
                    else
                    {
                        return (false, $"תשלום PayPal לא אושר. מצב: {stateValue}");
                    }
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to execute PayPal payment: {response.StatusCode} - {errorContent}");
                return (false, "שגיאה בביצוע תשלום PayPal.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing PayPal payment");
            return (false, "אירעה שגיאה בעת ביצוע תשלום PayPal.");
        }

        return (false, "לא ניתן לבצע תשלום PayPal.");
    }
}
