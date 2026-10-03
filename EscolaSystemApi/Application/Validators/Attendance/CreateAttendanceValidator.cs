using EscolaSystemApi.Application.DTOs.Attendance;
using FluentValidation;

namespace EscolaSystemApi.Application.Validators.Attendance;

public class CreateAttendanceValidator : AbstractValidator<CreateAttendanceDto>
{
    public CreateAttendanceValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.ClassId).NotEmpty();
        RuleFor(x => x.Date).LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)).WithMessage("Não é possível registrar chamada para datas futuras.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
