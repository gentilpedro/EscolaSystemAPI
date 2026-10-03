using EscolaSystemApi.Application.DTOs.Attendance;
using FluentValidation;

namespace EscolaSystemApi.Application.Validators.Attendance;

public class UpdateAttendanceValidator : AbstractValidator<UpdateAttendanceDto>
{
    public UpdateAttendanceValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
