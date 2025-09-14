using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using pureservice_dotnet.Models;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet.Services;

public interface IPureserviceCompanyService
{
    Task<Company?> AddCompany(string companyName);
    Task<CompanyDepartment?> AddDepartment(string departmentName, int companyId);
    Task<CompanyLocation?> AddLocation(string locationName, int companyId);
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
}