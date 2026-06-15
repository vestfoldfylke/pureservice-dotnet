using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using pureservice_dotnet.Models;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet.Services;

public interface IPureserviceTicketService
{
    Task<Ticket?> CreateTicket(TicketPayload payload);
    Task<TicketDepartment?> GetTicketDepartmentByName(string name, bool includeDisabled = false); 
    Task<TicketPriority?> GetTicketPriorityByName(string name, bool includeDisabled = false, int requestTypeId = 1);
    Task<TicketSource?> GetTicketSourceByName(string name, bool includeDisabled = false, int requestTypeId = 1);
    Task<TicketStatus?> GetTicketStatusByName(string name, bool includeDisabled = false, int requestTypeId = 1);
    Task<TicketType?> GetTicketTypeByName(string name, bool includeDisabled = false);
}

public class PureserviceTicketService : IPureserviceTicketService
{
    private readonly ILogger<PureserviceTicketService> _logger;
    private readonly IMetricsService _metricsService;
    private readonly IPureserviceCaller _pureserviceCaller;
    
    private const string DepartmentBasePath = "department";
    private const string PriorityBasePath = "priority";
    private const string SourceBasePath = "source";
    private const string StatusBasePath = "status";
    private const string TypeBasePath = "tickettype";
    private const string TicketBasePath = "ticket";

    public PureserviceTicketService(ILogger<PureserviceTicketService> logger, IMetricsService metricsService, IPureserviceCaller pureserviceCaller)
    {
        _logger = logger;
        _metricsService = metricsService;
        _pureserviceCaller = pureserviceCaller;
    }

    public async Task<Ticket?> CreateTicket(TicketPayload payload)
    {
        _logger.LogInformation("Creating ticket with Subject '{Subject}' from OriginatingReference: {OriginatingReference}", payload.Subject, payload.OriginatingReference);
        
        var ticketPayload = GetNewTicketPayload(payload);
        var result = await _pureserviceCaller.PostAsync<Ticket>(TicketBasePath, ticketPayload);

        if (result is not null)
        {
            _logger.LogInformation("Successfully created ticket with RequestNumber {RequestNumber} from OriginatingReference: {OriginatingReference}", result.RequestNumber, payload.OriginatingReference);
            _metricsService.Count($"{Constants.MetricsPrefix}_CreatedNewTicket", "Number of tickets created", (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue), ("OriginatingReference", payload.OriginatingReference));
            return result;
        }
        
        _logger.LogError("Failed to create ticket with Payload: {@Payload}", ticketPayload);
        _metricsService.Count($"{Constants.MetricsPrefix}_CreatedNewTicket", "Number of tickets created", (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue), ("OriginatingReference", payload.OriginatingReference));
        return null;
    }

    public async Task<TicketDepartment?> GetTicketDepartmentByName(string name, bool includeDisabled = false)
    {
        _logger.LogInformation("Getting department by name {DepartmentName}", name);
        var result = await _pureserviceCaller.GetAsync<TicketDepartmentList>($"{DepartmentBasePath}?filter=disabled == {includeDisabled} AND name == \"{name}\"");
        
        if (result is null)
        {
            _logger.LogError("Failed to get department with name {DepartmentName}", name);
            return null;
        }

        switch (result.Departments.Count)
        {
            case 0:
                _logger.LogError("No department found with name {DepartmentName}", name);
                return null;
            case > 1:
                _logger.LogWarning("Multiple departments found with name {DepartmentName}. Using first one. Departments: {@Departments}", name, result.Departments);
                return result.Departments.FirstOrDefault();
            default:
                return result.Departments.FirstOrDefault();
        }
    }

    public async Task<TicketPriority?> GetTicketPriorityByName(string name, bool includeDisabled = false, int requestTypeId = 1)
    {
        _logger.LogInformation("Getting priority by name {PriorityName} for RequestTypeId {RequestTypeId}", name, requestTypeId);
        var result = await _pureserviceCaller.GetAsync<TicketPriorityList>($"{PriorityBasePath}?filter=disabled == {includeDisabled} AND name == \"{name}\" AND requestTypeId == {requestTypeId}");
        
        if (result is null)
        {
            _logger.LogError("Failed to get priority with name {PriorityName} for RequestTypeId {RequestTypeId}", name, requestTypeId);
            return null;
        }

        switch (result.Priorities.Count)
        {
            case 0:
                _logger.LogError("No priority found with name {PriorityName} for RequestTypeId {RequestTypeId}", name, requestTypeId);
                return null;
            case > 1:
                _logger.LogWarning("Multiple priorities found with name {PriorityName} for RequestTypeId {RequestTypeId}. Using first one. Priorities: {@Priorities}", name, requestTypeId, result.Priorities);
                return result.Priorities.FirstOrDefault();
            default:
                return result.Priorities.FirstOrDefault();
        }
    }

    public async Task<TicketSource?> GetTicketSourceByName(string name, bool includeDisabled = false, int requestTypeId = 1)
    {
        _logger.LogInformation("Getting source by name {SourceName} for RequestTypeId {RequestTypeId}", name, requestTypeId);
        var result = await _pureserviceCaller.GetAsync<TicketSourceList>($"{SourceBasePath}?filter=disabled == {includeDisabled} AND name == \"{name}\" AND requestTypeId == {requestTypeId}");
        
        if (result is null)
        {
            _logger.LogError("Failed to get source with name {SourceName} for RequestTypeId {RequestTypeId}", name, requestTypeId);
            return null;
        }

        switch (result.Sources.Count)
        {
            case 0:
                _logger.LogError("No source found with name {SourceName} for RequestTypeId {RequestTypeId}", name, requestTypeId);
                return null;
            case > 1:
                _logger.LogWarning("Multiple sources found with name {SourceName} for RequestTypeId {RequestTypeId}. Using first one. Sources: {@Sources}", name, requestTypeId, result.Sources);
                return result.Sources.FirstOrDefault();
            default:
                return result.Sources.FirstOrDefault();
        }
    }

    public async Task<TicketStatus?> GetTicketStatusByName(string name, bool includeDisabled = false, int requestTypeId = 1)
    {
        _logger.LogInformation("Getting status by name {StatusName} for RequestTypeId {RequestTypeId}", name, requestTypeId);
        var result = await _pureserviceCaller.GetAsync<TicketStatusList>($"{StatusBasePath}?filter=disabled == {includeDisabled} AND name == \"{name}\" AND requestTypeId == {requestTypeId}");
        
        if (result is null)
        {
            _logger.LogError("Failed to get status with name {StatusName} for RequestTypeId {RequestTypeId}", name, requestTypeId);
            return null;
        }

        switch (result.Statuses.Count)
        {
            case 0:
                _logger.LogError("No status found with name {StatusName} for RequestTypeId {RequestTypeId}", name, requestTypeId);
                return null;
            case > 1:
                _logger.LogWarning("Multiple statuses found with name {StatusName} for RequestTypeId {RequestTypeId}. Using first one. Statuses: {@Statuses}", name, requestTypeId, result.Statuses);
                return result.Statuses.FirstOrDefault();
            default:
                return result.Statuses.FirstOrDefault();
        }
    }

    public async Task<TicketType?> GetTicketTypeByName(string name, bool includeDisabled = false)
    {
        _logger.LogInformation("Getting ticket type by name {TicketTypeName}", name);
        var result = await _pureserviceCaller.GetAsync<TicketTypeList>($"{TypeBasePath}?filter=disabled == {includeDisabled} AND name == \"{name}\"");
        
        if (result is null)
        {
            _logger.LogError("Failed to get ticket type with name {TicketTypeName}", name);
            return null;
        }

        switch (result.Tickettypes.Count)
        {
            case 0:
                _logger.LogError("No ticket type found with name {TicketTypeName}", name);
                return null;
            case > 1:
                _logger.LogWarning("Multiple ticket types found with name {TicketTypeName}. Using first one. TicketTypes: {@TicketTypes}", name, result.Tickettypes);
                return result.Tickettypes.FirstOrDefault();
            default:
                return result.Tickettypes.FirstOrDefault();
        }
    }
    
    private Dictionary<string, object?> GetNewTicketPayload(TicketPayload payload)
    {
        if (payload.Links.RequestType is null)
        {
            _logger.LogError("RequestType link is missing in payload when creating ticket");
            throw new InvalidOperationException("RequestType link is required to create a ticket");
        }
        
        if (payload.Links.User is null)
        {
            _logger.LogError("User link is missing in payload when creating ticket");
            throw new InvalidOperationException("User link is required to create a ticket");
        }
        
        if (payload.Links.TicketType is null)
        {
            _logger.LogError("TicketType link is missing in payload when creating ticket");
            throw new InvalidOperationException("TicketType link is required to create a ticket");
        }
        
        if (payload.Links.Priority is null)
        {
            _logger.LogError("Priority link is missing in payload when creating ticket");
            throw new InvalidOperationException("Priority link is required to create a ticket");
        }
        
        if (payload.Links.Status is null)
        {
            _logger.LogError("Status link is missing in payload when creating ticket");
            throw new InvalidOperationException("Status link is required to create a ticket");
        }
        
        if (payload.Links.Source is null)
        {
            _logger.LogError("Source link is missing in payload when creating ticket");
            throw new InvalidOperationException("Source link is required to create a ticket");
        }
        
        if (payload.Links.AssignedDepartment is null)
        {
            _logger.LogError("AssignedDepartment link is missing in payload when creating ticket");
            throw new InvalidOperationException("AssignedDepartment link is required to create a ticket");
        }
        
        var ticketPayload = new Dictionary<string, object?>
        {
            ["subject"] = payload.Subject,
            ["description"] = payload.Description,
            ["links"] = new Dictionary<string, Dictionary<string, int>>
            {
                ["requestType"] = new() { ["id"] = payload.Links.RequestType.Id },
                ["user"] = new() { ["id"] = payload.Links.User.Id },
                ["ticketType"] = new() { ["id"] = payload.Links.TicketType.Id },
                ["priority"] = new() { ["id"] = payload.Links.Priority.Id },
                ["status"] = new() { ["id"] = payload.Links.Status.Id },
                ["source"] = new() { ["id"] = payload.Links.Source.Id },
                ["assignedDepartment"] = new() { ["id"] = payload.Links.AssignedDepartment.Id }
            }
        };

        return new Dictionary<string, object?>
        {
            ["tickets"] = new List<object> { ticketPayload }
        };
    }
}