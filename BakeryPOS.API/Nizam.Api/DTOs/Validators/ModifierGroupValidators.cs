using Nizam.Api.DTOs;
using FluentValidation;

namespace Nizam.Api.DTOs.Validators;

/// <summary>
/// Cross-field validation for modifier group creation. DataAnnotations handle simple range
/// rules; this enforces <c>MaxSelect &gt;= MinSelect</c> (you can't require more than you allow).
/// Wired into the service layer via <c>IValidator&lt;ModifierGroupForCreateDto&gt;</c>.
/// </summary>
public sealed class ModifierGroupForCreateDtoValidator : AbstractValidator<ModifierGroupForCreateDto>
{
    public ModifierGroupForCreateDtoValidator()
    {
        RuleFor(x => x.MaxSelect)
            .GreaterThanOrEqualTo(x => x.MinSelect)
            .WithMessage("MaxSelect must be greater than or equal to MinSelect.");
    }
}

public sealed class ModifierGroupForUpdateDtoValidator : AbstractValidator<ModifierGroupForUpdateDto>
{
    public ModifierGroupForUpdateDtoValidator()
    {
        RuleFor(x => x.MaxSelect)
            .GreaterThanOrEqualTo(x => x.MinSelect)
            .WithMessage("MaxSelect must be greater than or equal to MinSelect.");
    }
}
