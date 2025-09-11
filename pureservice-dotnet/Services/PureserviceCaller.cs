using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet.Services;

public interface IPureserviceCaller
{
    Task<T?> GetAsync<T>(string endpoint) where T : class;
    (bool needsToWait, int requestCountLastMinute) NeedsToWait(int expectedRequestCount);
    Task<T?> PatchAsync<T>(string endpoint, object payload) where T : class;
    Task<bool> PatchAsync(string endpoint, object payload);
    Task<T?> PostAsync<T>(string endpoint, object payload) where T : class;
    Task<T?> PutAsync<T>(string endpoint, object payload) where T : class;
    Task<bool> PutAsync(string endpoint, object payload);
}

public class PureserviceCaller : IPureserviceCaller
{
    private readonly ILogger<PureserviceCaller> _logger;
    private readonly IMetricsService _metricsService;

    private readonly int _maxRequestsPerMinute;
    private readonly List<DateTime> _requestTimestamps;

    private readonly HttpClient _client;
    
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
    
    private const string MetricsServicePrefix = "API";
    
    public PureserviceCaller(IConfiguration configuration, ILogger<PureserviceCaller> logger, IMetricsService metricsService)
    {
        _logger = logger;
        _metricsService = metricsService;
        
        _maxRequestsPerMinute = configuration.GetValue<int?>("Pureservice_Max_Requests_Per_Minute") ?? throw new InvalidOperationException("Pureservice_Max_Requests_Per_Minute is not configured");
        
        _requestTimestamps = [];

        var baseUrl = configuration.GetValue<string>("Pureservice_BaseUrl") ?? throw new InvalidOperationException("Pureservice_BaseUrl is not configured");
        var apiKey = configuration.GetValue<string>("Pureservice_ApiKey") ?? throw new InvalidOperationException("Pureservice_ApiKey is not configured");

        _client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            DefaultRequestHeaders =
            {
                {
                    "X-Authorization-Key", apiKey
                }
            }
        };
    }
    
    public async Task<T?> GetAsync<T>(string endpoint) where T : class
    {
        AddAcceptHeaderIfMissing();
        
        var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        
        _logger.LogDebug("Sending GET request to Pureservice: {Endpoint}", endpoint);
        
        var (response, responseContent, statusCode, isSuccess) = await CallPureservice(request);

        try
        {
            response.EnsureSuccessStatusCode();

            var result = JsonSerializer.Deserialize<T>(responseContent, _jsonOptions)
                         ?? throw new InvalidOperationException("Deserialization returned null");

            _logger.LogDebug(
                "GET request successful to Pureservice: {Endpoint} with return type {Type} and StatusCode {StatusCode}",
                endpoint, typeof(T).Name, statusCode);

            return result;
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex,
                "ArgumentNullException when calling Pureservice GET: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HttpRequestException when calling Pureservice GET: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "JsonException when deserializing response from Pureservice GET: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "InvalidOperationException when calling Pureservice GET: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        finally
        {
            _metricsService.Count($"{Constants.MetricsPrefix}_{MetricsServicePrefix}_GetRequest", "Number of GET requests to Pureservice",
                (Constants.MetricsResultLabelName, isSuccess ? Constants.MetricsResultSuccessLabelValue : Constants.MetricsResultFailedLabelValue));
        }
    }

    public (bool needsToWait, int requestCountLastMinute) NeedsToWait(int expectedRequestCount)
    {
        var first = DateTime.Now.AddMinutes(-1);
        var last = DateTime.Now;
        var requestCountLastMinute = _requestTimestamps.Count(dt => dt >= first && dt <= last);
        
        return (requestCountLastMinute + expectedRequestCount > _maxRequestsPerMinute, requestCountLastMinute);
    }
    
    public async Task<T?> PatchAsync<T>(string endpoint, object payload) where T : class
    {
        AddAcceptHeaderIfMissing();
        
        _logger.LogDebug("Sending PATCH request to Pureservice: {Endpoint} with return type {Type}", endpoint, typeof(T).Name);
        
        var jsonPayload = JsonSerializer.Serialize(payload, _jsonOptions);
        var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Patch, endpoint)
        {
            Content = content
        };
        
        var (response, responseContent, statusCode, isSuccess) = await CallPureservice(request);

        try
        {
            response.EnsureSuccessStatusCode();

            var result = JsonSerializer.Deserialize<T>(responseContent, _jsonOptions)
                         ?? throw new InvalidOperationException("Deserialization returned null");

            _logger.LogDebug(
                "PATCH request successful to Pureservice: {Endpoint} with return type {Type} and StatusCode {StatusCode}",
                endpoint, typeof(T).Name, response.StatusCode);

            return result;
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex,
                "ArgumentNullException when calling Pureservice PATCH: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HttpRequestException when calling Pureservice PATCH: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "JsonException when deserializing response from Pureservice PATCH: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "InvalidOperationException when calling Pureservice PATCH: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        finally
        {
            _metricsService.Count($"{Constants.MetricsPrefix}_{MetricsServicePrefix}_PatchRequest", "Number of PATCH requests to Pureservice",
                (Constants.MetricsResultLabelName, isSuccess ? Constants.MetricsResultSuccessLabelValue : Constants.MetricsResultFailedLabelValue));
        }
    }
    
    public async Task<bool> PatchAsync(string endpoint, object payload)
    {
        AddAcceptHeaderIfMissing();
        
        _logger.LogDebug("Sending PATCH request to Pureservice: {Endpoint} without return type", endpoint);
        
        var jsonPayload = JsonSerializer.Serialize(payload, _jsonOptions);
        var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Patch, endpoint)
        {
            Content = content
        };

        var (response, responseContent, statusCode, isSuccess) = await CallPureservice(request);

        try
        {
            response.EnsureSuccessStatusCode();

            _logger.LogDebug(
                "PATCH request successful to Pureservice: {Endpoint} without return type and StatusCode {StatusCode}",
                endpoint, statusCode);

            return true;
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex,
                "ArgumentNullException when calling Pureservice PATCH: {Endpoint}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, statusCode, responseContent);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HttpRequestException when calling Pureservice PATCH: {Endpoint}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, statusCode, responseContent);
            return false;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "JsonException when deserializing response from Pureservice PATCH: {Endpoint}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, statusCode, responseContent);
            return false;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "InvalidOperationException when calling Pureservice PATCH: {Endpoint}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, statusCode, responseContent);
            return false;
        }
        finally
        {
            _metricsService.Count($"{Constants.MetricsPrefix}_{MetricsServicePrefix}_PatchRequest", "Number of PATCH requests to Pureservice",
                (Constants.MetricsResultLabelName, isSuccess ? Constants.MetricsResultSuccessLabelValue : Constants.MetricsResultFailedLabelValue));
        }
    }
    
    public async Task<T?> PostAsync<T>(string endpoint, object payload) where T : class
    {
        RemoveAcceptHeaderIfExists();
        
        _logger.LogDebug("Sending POST request to Pureservice: {Endpoint} with return type {Type}", endpoint, typeof(T).Name);
        
        var jsonPayload = JsonSerializer.Serialize(payload, _jsonOptions);
        var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/vnd.api+json");
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = content
        };
        
        var (response, responseContent, statusCode, isSuccess) = await CallPureservice(request);

        try
        {
            response.EnsureSuccessStatusCode();

            var result = JsonSerializer.Deserialize<T>(responseContent, _jsonOptions)
                         ?? throw new InvalidOperationException("Deserialization returned null");

            _logger.LogDebug(
                "POST request successful to Pureservice: {Endpoint} with return type {Type} and StatusCode {StatusCode}",
                endpoint, typeof(T).Name, statusCode);

            return result;
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex,
                "ArgumentNullException when calling Pureservice POST: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HttpRequestException when calling Pureservice POST: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "JsonException when deserializing response from Pureservice POST: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "InvalidOperationException when calling Pureservice POST: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        finally
        {
            _metricsService.Count($"{Constants.MetricsPrefix}_{MetricsServicePrefix}_PostRequest", "Number of POST requests to Pureservice",
                (Constants.MetricsResultLabelName, isSuccess ? Constants.MetricsResultSuccessLabelValue : Constants.MetricsResultFailedLabelValue));
        }
    }
    
    public async Task<T?> PutAsync<T>(string endpoint, object payload) where T : class
    {
        RemoveAcceptHeaderIfExists();
        
        _logger.LogDebug("Sending PUT request to Pureservice: {Endpoint} with return type {Type}", endpoint, typeof(T).Name);
        
        var jsonPayload = JsonSerializer.Serialize(payload, _jsonOptions);
        var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/vnd.api+json");
        var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
        {
            Content = content
        };
        
        var (response, responseContent, statusCode, isSuccess) = await CallPureservice(request);

        try
        {
            response.EnsureSuccessStatusCode();

            var result = JsonSerializer.Deserialize<T>(responseContent, _jsonOptions)
                         ?? throw new InvalidOperationException("Deserialization returned null");

            _logger.LogDebug(
                "PUT request successful to Pureservice: {Endpoint} with return type {Type} and StatusCode {StatusCode}",
                endpoint, typeof(T).Name, statusCode);

            return result;
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex,
                "ArgumentNullException when calling Pureservice PUT: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HttpRequestException when calling Pureservice PUT: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "JsonException when deserializing response from Pureservice PUT: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "InvalidOperationException when calling Pureservice PUT: {Endpoint} with return type {Type}. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, typeof(T).Name, statusCode, responseContent);
            return null;
        }
        finally
        {
            _metricsService.Count($"{Constants.MetricsPrefix}_{MetricsServicePrefix}_PutRequest", "Number of PUT requests to Pureservice",
                (Constants.MetricsResultLabelName, isSuccess ? Constants.MetricsResultSuccessLabelValue : Constants.MetricsResultFailedLabelValue));
        }
    }
    
    public async Task<bool> PutAsync(string endpoint, object payload)
    {
        RemoveAcceptHeaderIfExists();
        
        _logger.LogDebug("Sending PUT request to Pureservice: {Endpoint} without return type", endpoint);
        
        var jsonPayload = JsonSerializer.Serialize(payload, _jsonOptions);
        var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/vnd.api+json");
        var request = new HttpRequestMessage(HttpMethod.Put, endpoint)
        {
            Content = content
        };
        
        var (response, responseContent, statusCode, isSuccess) = await CallPureservice(request);

        try
        {
            response.EnsureSuccessStatusCode();

            _logger.LogDebug(
                "PUT request successful to Pureservice: {Endpoint} without return type and StatusCode {StatusCode}",
                endpoint, statusCode);

            return true;
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError(ex,
                "ArgumentNullException when calling Pureservice PUT: {Endpoint} without return type. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, statusCode, responseContent);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HttpRequestException when calling Pureservice PUT: {Endpoint} without return type. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, statusCode, responseContent);
            return false;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "JsonException when deserializing response from Pureservice PUT: {Endpoint} without return type. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, statusCode, responseContent);
            return false;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "InvalidOperationException when calling Pureservice PUT: {Endpoint} without return type. StatusCode: {StatusCode}. Content: {Content}",
                endpoint, statusCode, responseContent);
            return false;
        }
        finally
        {
            _metricsService.Count($"{Constants.MetricsPrefix}_{MetricsServicePrefix}_PutRequest", "Number of PUT requests to Pureservice",
                (Constants.MetricsResultLabelName, isSuccess ? Constants.MetricsResultSuccessLabelValue : Constants.MetricsResultFailedLabelValue));
        }
    }
    
    private void AddAcceptHeaderIfMissing()
    {
        if (!_client.DefaultRequestHeaders.Contains("Accept"))
        {
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
        }
    }
    
    private void RemoveAcceptHeaderIfExists()
    {
        if (_client.DefaultRequestHeaders.Contains("Accept"))
        {
            _client.DefaultRequestHeaders.Remove("Accept");
        }
    }
    
    private async Task<(HttpResponseMessage response, string content, int statusCode, bool isSuccess)> CallPureservice(
        HttpRequestMessage request)
    {
        HttpResponseMessage? response = null;
        string? content = null;
        var statusCode = 0;
        var isSuccess = false;

        try
        {
            _requestTimestamps.Add(DateTime.Now);
            
            response = await _client.SendAsync(request);
            content = await response.Content.ReadAsStringAsync();
            statusCode = (int)response.StatusCode;
            isSuccess = response.IsSuccessStatusCode;
            
            // Clean up old timestamps
            _requestTimestamps.RemoveAll(dt => dt < DateTime.Now.AddMinutes(-15));
            
            return (response, content, statusCode, isSuccess);
        }
        catch (Exception)
        {
            return (response!, content!, statusCode, isSuccess);
        }
    }
}