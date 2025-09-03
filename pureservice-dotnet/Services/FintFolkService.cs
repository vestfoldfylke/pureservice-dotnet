using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using pureservice_dotnet.Models.Fint;
using Vestfold.Extensions.Authentication.Services;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet.Services;

public class FintFolkService : IFintFolkService
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger<FintFolkService> _logger;
    private readonly IMetricsService _metricsService;
    
    private readonly MemoryCache _accessTokenCache = new MemoryCache(new MemoryCacheOptions());
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string[] _scopes;

    private const string AccessTokenCacheKey = "FINT";
    private const string MetricsServicePrefix = "FintFolk";

    public FintFolkService(IAuthenticationService authenticationService, IConfiguration configuration,
        ILogger<FintFolkService> logger, IMetricsService metricsService)
    {
        _authenticationService = authenticationService;
        _logger = logger;
        _metricsService = metricsService;

        var baseUrl = configuration.GetValue<string>("FintFolk_BaseUrl") ??
                      throw new InvalidOperationException("FintFolk_BaseUrl is not configured");

        var scopes = configuration.GetValue<string>("FintFolk_Scopes") ??
                     throw new InvalidOperationException("FintFolk_Scopes is not configured");

        _scopes = scopes.Split(",");
        if (_scopes.Length == 0)
        {
            throw new InvalidOperationException("FintFolk_Scope can not be an empty string");
        }
        
        _client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
    }

    public async Task<FintStudent?> GetStudent(string userPrincipalName) =>
        await GetAsync<FintStudent>($"student/upn/{userPrincipalName}?skipCache=true");
    
    private async Task<AccessToken> GetAccessToken()
    {
        if (_accessTokenCache.TryGetValue(AccessTokenCacheKey, out AccessToken? accessToken) && accessToken is not null)
        {
            return accessToken.Value;
        }

        accessToken = await _authenticationService.GetAccessToken(_scopes);

        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

        _accessTokenCache.Set(AccessTokenCacheKey, accessToken, cacheEntryOptions);

        return accessToken.Value;
    }

    private async Task<T?> GetAsync<T>(string endpoint) where T : class
    {
        var accessToken = await GetAccessToken();
        
        var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
        
        _logger.LogInformation("Sending GET request to FintFolk: {Endpoint}", endpoint);
        
        string? responseContent = null;
        var statusCode = 0;
        var isSuccess = false;

        try
        {
            var response = await _client.SendAsync(request);
            responseContent = await response.Content.ReadAsStringAsync();
            statusCode = (int)response.StatusCode;
            isSuccess = response.IsSuccessStatusCode;
            response.EnsureSuccessStatusCode();

            var result = JsonSerializer.Deserialize<T>(responseContent, _jsonOptions)
                         ?? throw new InvalidOperationException("Deserialization returned null");

            _logger.LogInformation(
                "GET request successful to FintFolk: {Endpoint} with return type {Type} and StatusCode {StatusCode}",
                endpoint, typeof(T).Name, statusCode);

            return result;
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex,
                "ArgumentNullException when calling FintFolk GET: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HttpRequestException when calling FintFolk GET: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "JsonException when deserializing response from FintFolk GET: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "InvalidOperationException when calling FintFolk GET: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        finally
        {
            _metricsService.Count($"{Constants.MetricsPrefix}_{MetricsServicePrefix}_GetRequest", "Number of GET requests to FintFolk",
                (Constants.MetricsResultLabelName, isSuccess ? Constants.MetricsResultSuccessLabelValue : Constants.MetricsResultFailedLabelValue));
        }
    }
}