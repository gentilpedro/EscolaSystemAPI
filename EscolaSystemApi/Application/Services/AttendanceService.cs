using EscolaSystemApi.Application.DTOs.Attendance;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Interfaces.Repositories;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaSystemApi.Application.Services;

public class AttendanceService(IUnitOfWork unitOfWork, AppDbContext context, ICurrentUserService currentUser) : IAttendanceService
{
    public async Task<Result<PagedResult<AttendanceDto>>> GetAllAsync(PagedQuery query, Guid? classId = null, Guid? studentId = null, DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        var baseQuery = context.Attendances.AsNoTracking()
            .Include(a => a.Student)
            .Include(a => a.Class).ThenInclude(c => c.School);

        var filtered = currentUser.Role switch
        {
            "Admin" => baseQuery,
            "Director" => baseQuery.Where(a => a.Class.SchoolId == currentUser.SchoolId),
            "Teacher" => baseQuery.Where(a => context.TeacherClasses
                .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == a.ClassId)),
            "Orientador" => baseQuery.Where(a => context.OrientadorClasses
                .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == a.ClassId)),
            "Student" => baseQuery.Where(a => a.StudentId == currentUser.StudentId),
            "Parent" => baseQuery.Where(a => context.ParentStudents
                .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == a.StudentId)),
            _ => baseQuery.Where(_ => false)
        };

        if (classId.HasValue)
            filtered = filtered.Where(a => a.ClassId == classId.Value);
        if (studentId.HasValue)
            filtered = filtered.Where(a => a.StudentId == studentId.Value);
        if (date.HasValue)
            filtered = filtered.Where(a => a.Date == date.Value);

        var totalCount = await filtered.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
        var data = await filtered.Skip(query.Skip).Take(query.Take).ToListAsync(cancellationToken);

        return Result<PagedResult<AttendanceDto>>.Success(
            new PagedResult<AttendanceDto>(data.Select(ToDto), query.Page, query.PageSize, totalCount, totalPages));
    }

    public async Task<Result<AttendanceDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = context.Attendances.AsNoTracking()
            .Include(a => a.Student)
            .Include(a => a.Class).ThenInclude(c => c.School);

        var filtered = currentUser.Role switch
        {
            "Admin" => query,
            "Director" => query.Where(a => a.Class.SchoolId == currentUser.SchoolId),
            "Teacher" => query.Where(a => context.TeacherClasses
                .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == a.ClassId)),
            "Orientador" => query.Where(a => context.OrientadorClasses
                .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == a.ClassId)),
            "Student" => query.Where(a => a.StudentId == currentUser.StudentId),
            "Parent" => query.Where(a => context.ParentStudents
                .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == a.StudentId)),
            _ => query.Where(_ => false)
        };

        var attendance = await filtered.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        return attendance is null
            ? Result<AttendanceDto>.NotFound("Chamada não encontrada.")
            : Result<AttendanceDto>.Success(ToDto(attendance));
    }

    public async Task<Result<AttendanceDto>> CreateAsync(CreateAttendanceDto dto, CancellationToken cancellationToken = default)
    {
        var student = await context.Students.Include(s => s.Class)
            .FirstOrDefaultAsync(s => s.Id == dto.StudentId, cancellationToken);

        if (student is null)
            return Result<AttendanceDto>.NotFound("Aluno não encontrado.");

        // Validar se o aluno pertence à turma informada
        if (student.ClassId != dto.ClassId)
            return Result<AttendanceDto>.BadRequest("Aluno não pertence a esta turma.");

        if (currentUser.Role == "Teacher")
        {
            var isTeacherOfClass = await context.TeacherClasses
                .AnyAsync(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == dto.ClassId, cancellationToken);

            if (!isTeacherOfClass)
                return Result<AttendanceDto>.Forbidden("Você não é professor desta turma.");
        }

        if (currentUser.Role == "Director" && student.Class.SchoolId != currentUser.SchoolId)
            return Result<AttendanceDto>.Forbidden("Este aluno não pertence à sua escola.");

        var duplicate = await unitOfWork.Repository<Attendance>()
            .ExistsAsync(a => a.StudentId == dto.StudentId && a.ClassId == dto.ClassId && a.Date == dto.Date, cancellationToken);

        if (duplicate)
            return Result<AttendanceDto>.Conflict("Chamada já registrada para este aluno nesta data.");

        var attendance = new Attendance
        {
            StudentId = dto.StudentId,
            ClassId = dto.ClassId,
            Date = dto.Date,
            IsPresent = dto.IsPresent,
            Notes = dto.Notes
        };

        await unitOfWork.Repository<Attendance>().AddAsync(attendance, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(attendance.Id, cancellationToken) is { IsSuccess: true } r
            ? Result<AttendanceDto>.Created(r.Data!)
            : Result<AttendanceDto>.BadRequest("Erro ao registrar chamada.");
    }

    public async Task<Result<AttendanceDto>> UpdateAsync(Guid id, UpdateAttendanceDto dto, CancellationToken cancellationToken = default)
    {
        var attendance = await context.Attendances.Include(a => a.Class)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (attendance is null)
            return Result<AttendanceDto>.NotFound("Chamada não encontrada.");

        if (currentUser.Role == "Teacher")
        {
            var isTeacherOfClass = await context.TeacherClasses
                .AnyAsync(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == attendance.ClassId, cancellationToken);

            if (!isTeacherOfClass)
                return Result<AttendanceDto>.Forbidden("Você não é professor desta turma.");
        }

        if (currentUser.Role == "Director" && attendance.Class.SchoolId != currentUser.SchoolId)
            return Result<AttendanceDto>.Forbidden("Esta chamada não pertence à sua escola.");

        attendance.IsPresent = dto.IsPresent;
        attendance.Notes = dto.Notes;

        unitOfWork.Repository<Attendance>().Update(attendance);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<Result<List<AttendanceDto>>> BulkCreateAsync(List<CreateAttendanceDto> dtos, CancellationToken cancellationToken = default)
    {
        if (dtos.Count == 0)
            return Result<List<AttendanceDto>>.BadRequest("Nenhuma chamada fornecida.");

        var classId = dtos[0].ClassId;
        if (dtos.Any(d => d.ClassId != classId))
            return Result<List<AttendanceDto>>.BadRequest("Todos os registros devem pertencer à mesma turma.");

        if (currentUser.Role == "Teacher")
        {
            var isTeacherOfClass = await context.TeacherClasses
                .AnyAsync(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == classId, cancellationToken);

            if (!isTeacherOfClass)
                return Result<List<AttendanceDto>>.Forbidden("Você não é professor desta turma.");
        }

        var date = dtos[0].Date;
        var studentIds = dtos.Select(d => d.StudentId).ToList();

        var existingIds = await context.Attendances
            .Where(a => a.ClassId == classId && a.Date == date && studentIds.Contains(a.StudentId))
            .Select(a => a.StudentId)
            .ToListAsync(cancellationToken);

        if (existingIds.Count > 0)
            return Result<List<AttendanceDto>>.Conflict("Chamada já registrada para um ou mais alunos nesta data.");

        var attendances = dtos.Select(dto => new Attendance
        {
            StudentId = dto.StudentId,
            ClassId = dto.ClassId,
            Date = dto.Date,
            IsPresent = dto.IsPresent,
            Notes = dto.Notes
        }).ToList();

        foreach (var attendance in attendances)
            await unitOfWork.Repository<Attendance>().AddAsync(attendance, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var ids = attendances.Select(a => a.Id).ToList();
        var created = await context.Attendances
            .Include(a => a.Student)
            .Include(a => a.Class).ThenInclude(c => c.School)
            .Where(a => ids.Contains(a.Id))
            .ToListAsync(cancellationToken);

        return Result<List<AttendanceDto>>.Created(created.Select(ToDto).ToList());
    }

    private static AttendanceDto ToDto(Attendance a) =>
        new(a.Id, a.StudentId, a.Student?.Name ?? string.Empty,
            a.ClassId, a.Class?.Name ?? string.Empty, a.Date, a.IsPresent, a.Notes, a.CreatedAt);
}