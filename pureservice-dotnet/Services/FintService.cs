using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using pureservice_dotnet.Models.Fint;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet.Services;

public class FintService : IFintService
{
    private readonly ILogger<FintService> _logger;
    private readonly IMetricsService _metricsService;
    
    private readonly MemoryCache _accessTokenCache = new MemoryCache(new MemoryCacheOptions());
    private readonly HttpClient _fintClient;
    private readonly HttpClient _fintTokenClient;
    private readonly JsonSerializerOptions _jsonTokenOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    private readonly string _feideNameDomain;
    private readonly FintAuthBody _fintAuthBody;

    private const string AccessTokenCacheKey = "FINT";
    private const string MetricsServicePrefix = "FINT";

    public FintService(IConfiguration configuration, ILogger<FintService> logger, IMetricsService metricsService)
    {
        _logger = logger;
        _metricsService = metricsService;

        var baseUrl = configuration.GetValue<string>("Fint_BaseUrl") ??
            throw new InvalidOperationException("Fint_BaseUrl is not configured");

        var clientId = configuration.GetValue<string>("Fint_Client_Id") ??
            throw new InvalidOperationException("Fint_Client_Id is not configured");

        var clientSecret = configuration.GetValue<string>("Fint_Client_Secret") ??
            throw new InvalidOperationException("Fint_Client_Secret is not configured");
        
        var username = configuration.GetValue<string>("Fint_Username") ??
            throw new InvalidOperationException("Fint_Username is not configured");
        
        var password = configuration.GetValue<string>("Fint_Password") ??
            throw new InvalidOperationException("Fint_Password is not configured");
        
        var tokenUrl = configuration.GetValue<string>("Fint_Token_Url") ??
            throw new InvalidOperationException("Fint_Token_Url is not configured");
        
        var fintScope = configuration.GetValue<string>("Fint_Scope") ??
            throw new InvalidOperationException("Fint_Scope is not configured");
        
        _fintClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
        
        _fintTokenClient = new HttpClient
        {
            BaseAddress = new Uri(tokenUrl)
        };

        _fintAuthBody = new FintAuthBody("password", username, password, clientId, clientSecret, fintScope);
        _feideNameDomain = configuration.GetValue<string>("Feide_Name_Domain") ??
            throw new InvalidOperationException("Feide_Name_Domain is not configured");
    }

    public async Task<JsonNode?> GetStudent(string userPrincipalName) =>
        await PostAsync<JsonNode>(new
        {
            query = @$"query {{
                elev(feidenavn: ""{GenerateFeideName(userPrincipalName)}"") {{
                    person {{
                        kontaktinformasjon {{
                            mobiltelefonnummer
                        }}
                    }}
                    kontaktinformasjon {{
                        mobiltelefonnummer
                    }}
                }}
            }}"
        });
    
    private async Task<FintAccessToken> GetFintAccessToken()
    {
        if (_accessTokenCache.TryGetValue(AccessTokenCacheKey, out FintAccessToken? accessToken) && accessToken is not null)
        {
            return accessToken;
        }

        try
        {
            var content = new FormUrlEncodedContent(GenerateFintAuthNamedValueCollection(_fintAuthBody));

            var responseMessage = await _fintTokenClient.PostAsync(string.Empty, content);
            responseMessage.EnsureSuccessStatusCode();
            
            var responseContent = await responseMessage.Content.ReadAsStringAsync();
            accessToken = JsonSerializer.Deserialize<FintAccessToken>(responseContent, _jsonTokenOptions)
                ?? throw new InvalidOperationException("Deserialization of FintAccessToken returned null");

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(accessToken.ExpiresIn));

            _accessTokenCache.Set(AccessTokenCacheKey, accessToken, cacheEntryOptions);

            return accessToken;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to get generate query string from FintAuthBody or deserialize FintAccessToken");
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HttpRequestException when requesting access token from FINT");
            throw;
        }
    }

    private async Task<JsonNode?> PostAsync<T>(object graphQlPayload) where T : class
    {
        var accessToken = await GetFintAccessToken();

        const string endpoint = "graphql/graphql";
        var stringContent = JsonSerializer.Serialize(graphQlPayload);
        var content = new StringContent(stringContent, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.AccessToken);
        
        _logger.LogInformation("Sending POST request to FINT: {Endpoint}", endpoint);
        
        string? responseContent = null;
        var statusCode = 0;
        var isSuccess = false;

        try
        {
            var response = await _fintClient.SendAsync(request);
            responseContent = await response.Content.ReadAsStringAsync();
            statusCode = (int)response.StatusCode;
            isSuccess = response.IsSuccessStatusCode;
            response.EnsureSuccessStatusCode();

            var result = JsonNode.Parse(responseContent) ?? throw new InvalidOperationException("JsonNode.Parse returned null");

            _logger.LogInformation(
                "POST request successful to FINT: {Endpoint} with return type {Type} and StatusCode {StatusCode}",
                endpoint, typeof(T).Name, statusCode);

            return result;
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex,
                "ArgumentNullException when calling FINT POST: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HttpRequestException when calling FINT POST: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "JsonException when deserializing response from FINT POST: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "InvalidOperationException when calling FINT POST: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        finally
        {
            _metricsService.Count($"{Constants.MetricsPrefix}_{MetricsServicePrefix}_GetRequest", "Number of POST requests to FINT",
                (Constants.MetricsResultLabelName, isSuccess ? Constants.MetricsResultSuccessLabelValue : Constants.MetricsResultFailedLabelValue));
        }
    }

    private static List<KeyValuePair<string, string>> GenerateFintAuthNamedValueCollection(FintAuthBody body)
    {
        var namedValueCollection = new List<KeyValuePair<string, string>>();
        var props = typeof(FintAuthBody).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        
        foreach (var prop in props)
        {
            var value = prop.GetValue(body)?.ToString();
            if (value is null)
            {
                continue;
            }
            
            var jsonAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
            var name = jsonAttr?.Name ?? prop.Name;
            
            namedValueCollection.Add(new KeyValuePair<string, string>(name, value));
        }

        return namedValueCollection;
    }
    
    private string GenerateFeideName(string userPrincipalName) =>
        $"{userPrincipalName.Split('@')[0]}{_feideNameDomain}";
}