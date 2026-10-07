using EscolaSystemApi.Application.DTOs.Tickets;
using FluentValidation;

namespace EscolaSystemApi.Application.Validators.Tickets;

public class CreateTicketValidator : AbstractValidator<CreateTicketDto>
{
    public CreateTicketValidator()
    {
        RuleFor(x => x.Type).IsInEnum().WithMessage("Escolha o tipo: bug, melhoria, dúvida ou outro.");
        RuleFor(x => x.Title).NotEmpty().MinimumLength(5).MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MinimumLength(10).MaximumLength(4000);
    }
}

public class CreateTicketMessageValidator : AbstractValidator<CreateTicketMessageDto>
{
    public CreateTicketMessageValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
    }
}

public class UpdateTicketStatusValidator : AbstractValidator<UpdateTicketStatusDto>
{
    public UpdateTicketStatusValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}
