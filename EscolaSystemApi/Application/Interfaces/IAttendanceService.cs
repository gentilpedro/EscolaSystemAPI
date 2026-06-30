using EscolaSystemApi.Application.DTOs.Attendance;
using EscolaSystemApi.Common;

namespace EscolaSystemApi.Application.Interfaces;

public interface IAttendanceService
{
    Task<Result<PagedResult<AttendanceDto>>> GetAllAsync(PagedQuery query, Guid? classId = null, Guid? studentId = null, DateOnly? date = null, CancellationToken cancellationToken = default);
    Task<Result<AttendanceDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<AttendanceDto>> CreateAsync(CreateAttendanceDto dto, CancellationToken cancellationToken = default);
    Task<Result<AttendanceDto>> UpdateAsync(Guid id, UpdateAttendanceDto dto, CancellationToken cancellationToken = default);
    Task<Result<List<AttendanceDto>>> BulkCreateAsync(List<CreateAttendanceDto> dtos, CancellationToken cancellationToken = default);
}