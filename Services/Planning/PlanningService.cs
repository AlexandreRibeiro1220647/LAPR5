using TodoApi.Models;
using TodoApi.Models.Staff;
using TodoApi.Models.OperationType;
using TodoApi.Models.Shared;
using TodoApi.DTOs;
using TodoApi.DTOs.OperationType;
using TodoApi.Infrastructure;
using TodoApi.Infrastructure.Staff;
using TodoApi.Infrastructure.OperationType;
using TodoApi.Infrastructure.OperationRequest;
using TodoApi.Mappers;
using TodoApi.Mappers.OperationType;
using TodoApi.Mappers.OperationRequest;
using TodoApi.Services;

namespace TodoApi.Services;

public class PlanningService : IPlanningService
{
    
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<IPlanningService> _logger;

    private readonly IOperationTypeRepository _operationTypeRepository;
    private OperationTypeMapper _mapperOpType = new OperationTypeMapper();

    private readonly IOperationRequestRepository _operationRequestRepository;
    private OperationRequestMapper _mapperOpRequest = new OperationRequestMapper();

    private readonly IStaffRepository _staffRepository;
    private StaffMapper _mapperStaff = new StaffMapper();

    private readonly IStaffScheduleRepository _staffScheduleRepository;
    private StaffScheduleMapper _mapperStaffSchedule = new StaffScheduleMapper();


    public PlanningService(IUnitOfWork unitOfWork, IOperationTypeRepository operationTypeRepository, ILogger<IPlanningService> logger, IOperationRequestRepository operationRepository, IStaffRepository staffRepository, IStaffScheduleRepository staffScheduleRepository)
    {
        this._unitOfWork = unitOfWork;
        this._operationTypeRepository = operationTypeRepository;
        this._logger = logger;
        this._operationRequestRepository = operationRepository;
        this._staffRepository = staffRepository;
        this._staffScheduleRepository = staffScheduleRepository;
    }
    
    public async Task<List<OperationTypeDurationDTO>> GetOperationTypeDurations()
    {
        // Fetch all operation types from the repository
        List<Models.OperationType.OperationType> operationTypes = await _operationTypeRepository.GetAllAsync();

    // Map to OperationTypeDurationDTO
        return operationTypes.Select(operationType =>
        {
            // Extract the estimated durations for anesthesia, surgery, and cleaning
            var durations = _mapperOpType.ToDto(operationType).EstimatedDuration;

        // Ensure there are at least three durations for the mapping
            if (durations.Count < 3)
            {
                throw new InvalidOperationException($"OperationType {operationType.Id} does not have sufficient duration data. - {durations[0]} , {durations[1]}");
            }

            return new OperationTypeDurationDTO(
                operationType.Id.AsString(),
                durations[0],  // Anesthesia duration
                durations[1],  // Surgery duration
                durations[2]   // Cleaning duration
            );
        }).ToList();
    }

    
    public async Task<List<OperationRequestTypeDTO>> GetOperationRequestTypes()
    {
        try
        {
            List<Models.OperationRequest.OperationRequest> requests = await _operationRequestRepository.GetAllAsync();
            List<OperationRequestDTO> requestsDTO = new List<OperationRequestDTO>();
            foreach (Models.OperationRequest.OperationRequest operation in requests)
            {
                requestsDTO.Add(_mapperOpRequest.ToDto(operation));
            }

            List<OperationRequestTypeDTO> result = new List<OperationRequestTypeDTO>();

            foreach (OperationRequestDTO opR in requestsDTO)
            {
                result.Add(new OperationRequestTypeDTO(opR.operationId, opR.operationTypeId));
            }

            return result;
        }
        catch (Exception e)
        {
            this._logger.LogError(e, "Error getting operation request - type relation");
            throw;
        }
    }

    public async Task<List<OperationRequestDoctorDTO>> GetOperationRequestDoctors()
    {
    // Fetch all operation requests from the repository
        List<Models.OperationRequest.OperationRequest> requests = await _operationRequestRepository.GetAllAsync();

    // Map to OperationRequestDoctorDTO
        return requests.Select(opR =>
        {
            return new OperationRequestDoctorDTO(
                opR.Id.AsString(),
                opR.DoctorId.AsString()  // Cleaning duration
            );
        }).ToList();
    }

    public async Task<List<DoctorOperationTypesDTO>> GetDoctorOperationTypes()
    {
        // Fetch all operation types from the repository
        List<Models.OperationType.OperationType> operationTypes = await _operationTypeRepository.GetAllAsync();

        List<OperationTypeDTO> typesDTO = new List<OperationTypeDTO>();
        foreach (Models.OperationType.OperationType type in operationTypes)
        {
            typesDTO.Add(_mapperOpType.ToDto(type));
        }

        List<Staff> staffs = await _staffRepository.GetAllAsync();

        var doctorStaffs = staffs.Where(staff => 
            staff.user.Role.Equals("2", StringComparison.OrdinalIgnoreCase)
        );

    // Map to DoctorOperationTypesDTO
        return doctorStaffs.Select(staff =>
        {
                // Extract the estimated durations for anesthesia, surgery, and cleaning
                var specialization = _mapperStaff.ToDto(staff).Specialization;

                // Find operation types where the specialization is required
                List<string> sOpTypes = typesDTO
                    .Where(opT => opT.RequiredStaffBySpecialization.Any(item => item.Contains(specialization)))
                    .Select(opT => opT.OperationTypeId)
                    .Distinct() // Ensure no duplicates
                    .ToList();

                return new DoctorOperationTypesDTO(
                    staff.Id.AsString(),
                    staff.user.Role,
                    specialization,
                    sOpTypes
                );
        
        }).ToList();
    }

    public async Task<List<StaffScheduleDTO>> GetStaffSchedules()
    {
        List<StaffSchedule> staffSchedules = await _staffScheduleRepository.GetAllAsync();
        return staffSchedules.Select(staffSchedule => _mapperStaffSchedule.ToDto(staffSchedule)).ToList();
    }
}