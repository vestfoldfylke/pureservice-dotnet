using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using pureservice_dotnet.Models;
using pureservice_dotnet.Models.Enums;
using pureservice_dotnet.Services;

namespace pureservice_dotnet.Functions;

public class TicketFunctions
{
    private readonly ILogger<TicketFunctions> _logger;
    private readonly IPureserviceEmailAddressService _pureserviceEmailAddressService;
    private readonly IPureservicePhoneNumberService _pureservicePhoneNumberService;
    private readonly IPureserviceTicketService _pureserviceTicketService;
    private readonly IPureserviceUserService _pureserviceUserService;

    private readonly string _ticketExtraInformation;
    private readonly string _ticketTypeFallbackName;
    private readonly string _ticketPriorityFallbackName;
    private readonly string _ticketStatusFallbackName;
    private readonly string _ticketSourceFallbackName;
    private readonly string _ticketAssignedDepartmentFallbackName;

    public TicketFunctions(IConfiguration configuration, ILogger<TicketFunctions> logger, IPureserviceEmailAddressService pureserviceEmailAddressService, IPureservicePhoneNumberService pureservicePhoneNumberService,
        IPureserviceTicketService pureserviceTicketService, IPureserviceUserService pureserviceUserService)
    {
        _ticketExtraInformation = configuration["Pureservice_Ticket_ExtraInformation"] ?? "Ekstra informasjon";
        _ticketTypeFallbackName = configuration["Pureservice_Ticket_FallbackTypeName"] ?? "";
        _ticketPriorityFallbackName = configuration["Pureservice_Ticket_FallbackPriorityName"] ?? "";
        _ticketStatusFallbackName = configuration["Pureservice_Ticket_FallbackStatusName"] ?? "";
        _ticketSourceFallbackName = configuration["Pureservice_Ticket_FallbackSourceName"] ?? "";
        _ticketAssignedDepartmentFallbackName = configuration["Pureservice_Ticket_FallbackAssignedDepartmentName"] ?? "";
        
        _logger = logger;
        _pureserviceEmailAddressService = pureserviceEmailAddressService;
        _pureservicePhoneNumberService = pureservicePhoneNumberService;
        _pureserviceTicketService = pureserviceTicketService;
        _pureserviceUserService = pureserviceUserService;
    }

    [Function("create")]
    public async Task<IActionResult> CreateTicket([HttpTrigger(AuthorizationLevel.Function, "post", Route = "tickets/create")] HttpRequest req)
    {
        var payload = await DeserializePayload(req);
        if (payload is null)
        {
            _logger.LogWarning("Failed to create ticket: Request body is missing or has invalid JSON");
            return new BadRequestObjectResult("Invalid request body. Send valid JSON for CreateTicketPayload fields.");
        }
        
        User? user;
        try
        {
            user = await GetOrCreateUser(payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get or create user with EmailAddress {EmailAddress} for ticket creation", payload.User.EmailAddress);
            return new BadRequestObjectResult(ex.Message);
        }
        
        var ticketType = await _pureserviceTicketService.GetTicketTypeByName(payload.TicketMetaData.TicketTypeName);
        if (ticketType is null)
        {
            ticketType = await _pureserviceTicketService.GetTicketTypeByName(_ticketTypeFallbackName);
            if (ticketType is null)
            {
                _logger.LogError("TicketType with name {TicketTypeName} and fallback name {TicketTypeFallbackName} not found for ticket creation", payload.TicketMetaData.TicketTypeName, _ticketTypeFallbackName);
                throw new InvalidOperationException($"TicketType with name {payload.TicketMetaData.TicketTypeName} and fallback name {_ticketTypeFallbackName} not found");
            }
            
            _logger.LogWarning("Ticket type with name {TicketTypeName} not found for ticket creation. Using fallback TicketTypeName {FallbackTicketTypeName} with TicketTypeId {TicketTypeId}", payload.TicketMetaData.TicketTypeName, _ticketTypeFallbackName, ticketType.Id);
        }
        
        var priority = await _pureserviceTicketService.GetTicketPriorityByName(payload.TicketMetaData.PriorityName, requestTypeId: payload.TicketMetaData.RequestTypeId);
        if (priority is null)
        {
            priority = await _pureserviceTicketService.GetTicketPriorityByName(_ticketPriorityFallbackName, requestTypeId: payload.TicketMetaData.RequestTypeId);
            if (priority is null)
            {
                _logger.LogError("Priority with name {PriorityName} and fallback name {PriorityFallbackName} not found for ticket creation", payload.TicketMetaData.PriorityName, _ticketPriorityFallbackName);
                throw new InvalidOperationException($"Priority with name {payload.TicketMetaData.PriorityName} and fallback name {_ticketPriorityFallbackName} not found");
            }
            
            _logger.LogWarning("Priority with name {PriorityName} not found for ticket creation. Using fallback PriorityName {FallbackPriorityName} with PriorityId {PriorityId}", payload.TicketMetaData.PriorityName, _ticketPriorityFallbackName, priority.Id);
        }
        
        var status = await _pureserviceTicketService.GetTicketStatusByName(payload.TicketMetaData.StatusName,  requestTypeId: payload.TicketMetaData.RequestTypeId);
        if (status is null)
        {
            status = await _pureserviceTicketService.GetTicketStatusByName(_ticketStatusFallbackName, requestTypeId: payload.TicketMetaData.RequestTypeId);
            if (status is null)
            {
                _logger.LogError("Status with name {StatusName} and fallback name {StatusFallbackName} not found for ticket creation", payload.TicketMetaData.StatusName, _ticketStatusFallbackName);
                throw new InvalidOperationException($"Status with name {payload.TicketMetaData.StatusName} and fallback name {_ticketStatusFallbackName} not found");
            }
            
            _logger.LogWarning("Status with name {StatusName} not found for ticket creation. Using fallback StatusName {FallbackStatusName} with StatusId {StatusId}", payload.TicketMetaData.StatusName, _ticketStatusFallbackName, status.Id);
        }
        
        var source = await _pureserviceTicketService.GetTicketSourceByName(payload.TicketMetaData.SourceName, requestTypeId: payload.TicketMetaData.RequestTypeId);
        if (source is null)
        {
            source = await _pureserviceTicketService.GetTicketSourceByName(_ticketSourceFallbackName, requestTypeId: payload.TicketMetaData.RequestTypeId);
            if (source is null)
            {
                _logger.LogError("Source with name {SourceName} and fallback name {SourceFallbackName} not found for ticket creation", payload.TicketMetaData.SourceName, _ticketSourceFallbackName);
                throw new InvalidOperationException($"Source with name {payload.TicketMetaData.SourceName} and fallback name {_ticketSourceFallbackName} not found");
            }
            
            _logger.LogWarning("Source with name {SourceName} not found for ticket creation. Using fallback SourceName {FallbackSourceName} with SourceId {SourceId}", payload.TicketMetaData.SourceName, _ticketSourceFallbackName, source.Id);
        }
        
        var assignedDepartment = await _pureserviceTicketService.GetTicketDepartmentByName(payload.TicketMetaData.AssignedDepartmentName);
        if (assignedDepartment is null)
        {
            assignedDepartment = await _pureserviceTicketService.GetTicketDepartmentByName(_ticketAssignedDepartmentFallbackName);
            if (assignedDepartment is null)
            {
                _logger.LogError("AssignedDepartment with name {AssignedDepartmentName} and fallback name {AssignedDepartmentFallbackName} not found for ticket creation", payload.TicketMetaData.AssignedDepartmentName, _ticketAssignedDepartmentFallbackName);
                throw new InvalidOperationException($"AssignedDepartment with name {payload.TicketMetaData.AssignedDepartmentName} and fallback name {_ticketAssignedDepartmentFallbackName} not found");
            }
            
            _logger.LogWarning("AssignedDepartment with name {AssignedDepartmentName} not found for ticket creation. Using fallback AssignedDepartmentName {FallbackAssignedDepartmentName} with AssignedDepartmentId {AssignedDepartmentId}", payload.TicketMetaData.AssignedDepartmentName, _ticketAssignedDepartmentFallbackName, assignedDepartment.Id);
        }

        var ticketPayload = new TicketPayload
        {
            Description = GetTicketDescription(payload),
            OriginatingReference = payload.OriginatingReference,
            Subject = payload.TicketMetaData.Subject,
            Links = new Links
            {
                RequestType = new Link(payload.TicketMetaData.RequestTypeId, ""),
                User = new Link(user.Id, ""),
                TicketType = new Link(ticketType.Id, ""),
                Priority = new Link(priority.Id, ""),
                Status = new Link(status.Id, ""),
                Source = new Link(source.Id, ""),
                AssignedDepartment = new Link(assignedDepartment.Id, "")
            }
        };
        var ticket = await _pureserviceTicketService.CreateTicket(ticketPayload);

        if (ticket is null)
        {
            _logger.LogError("Failed to create ticket linked to UserId {UserId} with EmailAddress {EmailAddress} from OriginatingReference {OriginatingReference}", user.Id, payload.User.EmailAddress, payload.OriginatingReference);
            return new BadRequestObjectResult($"Failed to create ticket linked to UserId {user.Id} with EmailAddress {payload.User.EmailAddress} from OriginatingReference {payload.OriginatingReference}");
        }
        
        _logger.LogInformation("Created ticket linked to UserId {UserId} with EmailAddress {EmailAddress} from OriginatingReference {OriginatingReference}", user.Id, payload.User.EmailAddress, payload.OriginatingReference);
        return new JsonResult(ticket);
    }

    private static async Task<CreateTicketPayload?> DeserializePayload(HttpRequest req)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<CreateTicketPayload>(
                req.Body,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<User> GetOrCreateUser(CreateTicketPayload payload)
    {
        var user = await _pureserviceUserService.GetUserByEmailAddress(payload.User.EmailAddress);
        if (user is not null)
        {
            if (!user.Disabled)
            {
                _logger.LogInformation("User with EmailAddress {EmailAddress} found on UserId {UserId}", payload.User.EmailAddress, user.Id);
                return user;
            }
            
            _logger.LogWarning("User with email address {EmailAddress} is disabled. Re-enabling user", payload.User.EmailAddress);
            var userReEnabled = await _pureserviceUserService.UpdateBasicProperties(user.Id, [("disabled", (null, null, false))]);
            if (!userReEnabled)
            {
                _logger.LogError("Failed to re-enable user with email address {EmailAddress} and UserId {UserId}", payload.User.EmailAddress, user.Id);
                throw new InvalidOperationException($"Failed to re-enable user with email address {payload.User.EmailAddress} and UserId {user.Id}");
            }
                
            _logger.LogInformation("User with email address {EmailAddress} and UserId {UserId} re-enabled", payload.User.EmailAddress, user.Id);
            return user;
        }
        
        _logger.LogWarning("User with EmailAddress {EmailAddress} not found. Creating manual user for {@User}", payload.User.EmailAddress, payload.User);
        
        var nameSplit = payload.User.Name.Split(' ');
        var givenName = nameSplit.Length > 1 ? string.Join(' ', nameSplit.Take(nameSplit.Length - 1)) : payload.User.Name;
        var surname = nameSplit.Length > 1 ? nameSplit.Last() : payload.User.Name;
        
        var phonenumber = await _pureservicePhoneNumberService.AddNewPhoneNumber(payload.User.PhoneNumber, PhoneNumberType.Mobile);
        if (phonenumber is null)
        {
            throw new InvalidOperationException($"Failed to create phone number {payload.User.PhoneNumber}");
        }
        
        var emailAddress = await _pureserviceEmailAddressService.AddNewEmailAddress(payload.User.EmailAddress);
        if (emailAddress is null)
        {
            throw new InvalidOperationException($"Failed to create email address {payload.User.EmailAddress}");
        }

        var notes = $"OriginatingReference: {payload.OriginatingReference}";

        user = await _pureserviceUserService.CreateManualUser(givenName, surname, null, phonenumber.Id, emailAddress.Id, notes);
        
        return user ?? throw new InvalidOperationException($"User with EmailAddress {payload.User.EmailAddress} failed to be created");
    }

    private string GetTicketDescription(CreateTicketPayload payload)
    {
        var description = $"<p>{payload.TicketMetaData.Description}</p>\n<p>&nbsp;</p>";
        var originatingReference = $"<p><strong>Referanse:</strong><br>{payload.OriginatingReference}</p>";
        
        return payload.AdditionalData is null
            ? $"{description}\n{originatingReference}"
            : $"{description}\n<p><strong>{_ticketExtraInformation}</strong>:<br>{string.Join("<br>", payload.AdditionalData.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}</p>\n<p>&nbsp;</p>\n{originatingReference}";
    }
}