using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using pureservice_dotnet.Models;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet.Services;

public interface IPureserviceCompanyService
{
    Task<Company?> AddCompany(string companyName);
    Task<CompanyDepartment?> AddDepartment(string departmentName, int companyId);
    Task<CompanyLocation?> AddLocation(string locationName, int companyId);
    Task<List<Company>> GetCompanies(int start = 0, int limit = 500);
    Task<List<CompanyDepartment>> GetDepartments(int start = 0, int limit = 500);
    Task<List<CompanyLocation>> GetLocations(int start = 0, int limit = 500);
}

public class PureserviceCompanyService : IPureserviceCompanyService
{
    private readonly ILogger<PureserviceCompanyService> _logger;
    private readonly IMetricsService _metricsService;
    private readonly IPureserviceCaller _pureserviceCaller;
    
    private const string CompanyBasePath = "company";
    private const string DepartmentBasePath = "companydepartment";
    private const string LocationBasePath = "companylocation";
    
    public PureserviceCompanyService(ILogger<PureserviceCompanyService> logger, IMetricsService metricsService, IPureserviceCaller pureserviceCaller)
    {
        _logger = logger;
        _metricsService = metricsService;
        _pureserviceCaller = pureserviceCaller;
    }

    public async Task<Company?> AddCompany(string companyName)
    {
        var payload = new
        {
            companies = new[]
            {
                new
                {
                    name = companyName,
                    disabled = false
                }
            }
        };
        
        _logger.LogInformation("Creating company");
        var result = await _pureserviceCaller.PostAsync<Company>($"{CompanyBasePath}", payload);
        
        if (result is not null)
        {
            _logger.LogInformation("Successfully created company with CompanyId {CompanyId}", result.Id);
            _metricsService.Count($"{Constants.MetricsPrefix}_CompanyCreated", "Number of companies created",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return result;
        }
        
        _logger.LogError("Failed to create company: {@Payload}", payload);
        _metricsService.Count($"{Constants.MetricsPrefix}_CompanyCreated", "Number of companies created",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return null;
    }

    public async Task<CompanyDepartment?> AddDepartment(string departmentName, int companyId)
    {
        var payload = new
        {
            companydepartments = new[]
            {
                new
                {
                    name = departmentName,
                    links = new
                    {
                        company = new
                        {
                            id = companyId,
                            type = "company"
                        }
                    }
                }
            }
        };
        
        _logger.LogInformation("Creating department under CompanyId {CompanyId}", companyId);
        var result = await _pureserviceCaller.PostAsync<CompanyDepartment>($"{DepartmentBasePath}", payload);
        
        if (result is not null)
        {
            _logger.LogInformation("Successfully created department with DepartmentId {DepartmentId} under CompanyId {CompanyId}", result.Id, companyId);
            _metricsService.Count($"{Constants.MetricsPrefix}_DepartmentCreated", "Number of departments created",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return result;
        }
        
        _logger.LogError("Failed to create department under CompanyId {CompanyId}: {@Payload}", companyId, payload);
        _metricsService.Count($"{Constants.MetricsPrefix}_DepartmentCreated", "Number of departments created",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return null;
    }
    
    public async Task<CompanyLocation?> AddLocation(string locationName, int companyId)
    {
        var payload = new
        {
            companylocations = new[]
            {
                new
                {
                    name = locationName,
                    links = new
                    {
                        company = new
                        {
                            id = companyId,
                            type = "company"
                        }
                    }
                }
            }
        };
        
        _logger.LogInformation("Creating location under CompanyId {CompanyId}", companyId);
        var result = await _pureserviceCaller.PostAsync<CompanyLocation>($"{LocationBasePath}", payload);
        
        if (result is not null)
        {
            _logger.LogInformation("Successfully created location with LocationId {LocationId} under CompanyId {CompanyId}", result.Id, companyId);
            _metricsService.Count($"{Constants.MetricsPrefix}_LocationCreated", "Number of locations created",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return result;
        }
        
        _logger.LogError("Failed to create department under CompanyId {CompanyId}: {@Payload}", companyId, payload);
        _metricsService.Count($"{Constants.MetricsPrefix}_LocationCreated", "Number of locations created",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return null;
    }
    
    public async Task<List<Company>> GetCompanies(int start = 0, int limit = 500)
    {
        var companyList = new List<Company>();

        var currentStart = start;
        
        var queryString = HttpUtility.ParseQueryString(string.Empty);

        queryString["start"] = currentStart.ToString();
        queryString["limit"] = limit.ToString();

        CompanyList? result = null;
        
        while (result is null || result.Companies.Count > 0)
        {
            queryString["start"] = currentStart.ToString();
            
            result = await _pureserviceCaller.GetAsync<CompanyList>($"{CompanyBasePath}?{queryString}");
            if (result is null)
            {
                _logger.LogError("No result returned from API from start {Start} with limit {Limit}", currentStart, limit);
                return companyList;
            }
            
            _logger.LogDebug("Fetched {Count} Pureservice companies starting from {Start} with limit {Limit}", result.Companies.Count, currentStart, limit);
            companyList.AddRange(result.Companies);
            
            if (result.Companies.Count == 0)
            {
                _logger.LogInformation("Returning {CompanyCount} Pureservice companies", companyList.Count);
                return companyList;
            }

            currentStart += result.Companies.Count;
            _logger.LogDebug("Preparing to fetch next batch of Pureservice companies starting from start {Start} and limit {Limit}", currentStart, limit);
        }
        
        _logger.LogWarning("Reached outside of while somehow 😱 Returning {CompanyCount} Pureservice companies", companyList.Count);
        return companyList;
    }
    
    public async Task<List<CompanyDepartment>> GetDepartments(int start = 0, int limit = 500)
    {
        var departmentList = new List<CompanyDepartment>();

        var currentStart = start;
        
        var queryString = HttpUtility.ParseQueryString(string.Empty);

        queryString["start"] = currentStart.ToString();
        queryString["limit"] = limit.ToString();

        CompanyDepartmentList? result = null;
        
        while (result is null || result.Companydepartments.Count > 0)
        {
            queryString["start"] = currentStart.ToString();
            
            result = await _pureserviceCaller.GetAsync<CompanyDepartmentList>($"{DepartmentBasePath}?{queryString}");
            if (result is null)
            {
                _logger.LogError("No result returned from API from start {Start} with limit {Limit}", currentStart, limit);
                return departmentList;
            }
            
            _logger.LogDebug("Fetched {Count} Pureservice departments starting from {Start} with limit {Limit}", result.Companydepartments.Count, currentStart, limit);
            departmentList.AddRange(result.Companydepartments);
            
            if (result.Companydepartments.Count == 0)
            {
                _logger.LogInformation("Returning {DepartmentCount} Pureservice departments", departmentList.Count);
                return departmentList;
            }

            currentStart += result.Companydepartments.Count;
            _logger.LogDebug("Preparing to fetch next batch of Pureservice departments starting from start {Start} and limit {Limit}", currentStart, limit);
        }
        
        _logger.LogWarning("Reached outside of while somehow 😱 Returning {DepartmentCount} Pureservice departments", departmentList.Count);
        return departmentList;
    }
    
    public async Task<List<CompanyLocation>> GetLocations(int start = 0, int limit = 500)
    {
        var locationList = new List<CompanyLocation>();

        var currentStart = start;
        
        var queryString = HttpUtility.ParseQueryString(string.Empty);

        queryString["start"] = currentStart.ToString();
        queryString["limit"] = limit.ToString();

        CompanyLocationList? result = null;
        
        while (result is null || result.Companylocations.Count > 0)
        {
            queryString["start"] = currentStart.ToString();
            
            result = await _pureserviceCaller.GetAsync<CompanyLocationList>($"{LocationBasePath}?{queryString}");
            if (result is null)
            {
                _logger.LogError("No result returned from API from start {Start} with limit {Limit}", currentStart, limit);
                return locationList;
            }
            
            _logger.LogDebug("Fetched {Count} Pureservice locations starting from {Start} with limit {Limit}", result.Companylocations.Count, currentStart, limit);
            locationList.AddRange(result.Companylocations);
            
            if (result.Companylocations.Count == 0)
            {
                _logger.LogInformation("Returning {LocationCount} Pureservice locations", locationList.Count);
                return locationList;
            }

            currentStart += result.Companylocations.Count;
            _logger.LogDebug("Preparing to fetch next batch of Pureservice locations starting from start {Start} and limit {Limit}", currentStart, limit);
        }
        
        _logger.LogWarning("Reached outside of while somehow 😱 Returning {LocationCount} Pureservice locations", locationList.Count);
        return locationList;
    }
}