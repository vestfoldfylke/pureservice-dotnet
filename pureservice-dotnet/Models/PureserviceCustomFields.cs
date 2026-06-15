using System.Text.Json.Serialization;

namespace pureservice_dotnet.Models;

public class PureserviceCustomFields
{
    /// <summary>
    /// CustomField1 can be one of the following types based on what the Custom Field is used for in Pureservice:<br />
    /// - string<br />
    /// - int<br />
    /// - DateTime
    /// </summary>
    [JsonPropertyName("cf_1")]
    public object? CustomField1 { get; init; }
    
    /// <summary>
    /// CustomField2 can be one of the following types based on what the Custom Field is used for in Pureservice:<br />
    /// - string<br />
    /// - int<br />
    /// - DateTime
    /// </summary>
    [JsonPropertyName("cf_2")]
    public object? CustomField2 { get; init; }
    
    /// <summary>
    /// CustomField3 can be one of the following types based on what the Custom Field is used for in Pureservice:<br />
    /// - string<br />
    /// - int<br />
    /// - DateTime
    /// </summary>
    [JsonPropertyName("cf_3")]
    public object? CustomField3 { get; init; }
    
    /// <summary>
    /// CustomField4 can be one of the following types based on what the Custom Field is used for in Pureservice:<br />
    /// - string<br />
    /// - int<br />
    /// - DateTime
    /// </summary>
    [JsonPropertyName("cf_4")]
    public object? CustomField4 { get; init; }
    
    /// <summary>
    /// CustomField5 can be one of the following types based on what the Custom Field is used for in Pureservice:<br />
    /// - string<br />
    /// - int<br />
    /// - DateTime
    /// </summary>
    [JsonPropertyName("cf_5")]
    public object? CustomField5 { get; init; }
    
    /// <summary>
    /// CustomField6 can be one of the following types based on what the Custom Field is used for in Pureservice:<br />
    /// - string<br />
    /// - int<br />
    /// - DateTime
    /// </summary>
    [JsonPropertyName("cf_6")]
    public object? CustomField6 { get; init; }
    
    /// <summary>
    /// CustomField7 can be one of the following types based on what the Custom Field is used for in Pureservice:<br />
    /// - string<br />
    /// - int<br />
    /// - DateTime
    /// </summary>
    [JsonPropertyName("cf_7")]
    public object? CustomField7 { get; init; }
    
    /// <summary>
    /// CustomField8 can be one of the following types based on what the Custom Field is used for in Pureservice:<br />
    /// - string<br />
    /// - int<br />
    /// - DateTime
    /// </summary>
    [JsonPropertyName("cf_8")]
    public object? CustomField8 { get; init; }
    
    /// <summary>
    /// CustomField9 can be one of the following types based on what the Custom Field is used for in Pureservice:<br />
    /// - string<br />
    /// - int<br />
    /// - DateTime
    /// </summary>
    [JsonPropertyName("cf_9")]
    public object? CustomField9 { get; init; }
}