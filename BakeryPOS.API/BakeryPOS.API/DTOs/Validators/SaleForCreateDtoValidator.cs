using BakeryPOS.API.Core.Enums;
using BakeryPOS.API.DTOs;
using FluentValidation;

namespace BakeryPOS.API.DTOs.Validators;

/// <summary>
/// First showcase of the FluentValidation pattern. Other DTOs migrate over time —
/// new DTOs should use this style; existing DTOs keep DataAnnotations until refactored.
/// </summary>
public sealed class SaleForCreateDtoValidator : AbstractValidator<SaleForCreateDto>
{
    public SaleForCreateDtoValidator()
    {
        RuleFor(x => x.SaleDetails)
            .NotEmpty().WithMessage("La vente doit contenir au moins un article.");

        RuleForEach(x => x.SaleDetails).SetValidator(new SaleDetailForCreateDtoValidator());

        RuleFor(x => x.AmountPaid)
            .GreaterThanOrEqualTo(0).WithMessage("Le montant payé ne peut pas être négatif.");

        // Split payment requires both halves to be specified.
        When(x => x.PaymentMethod == PaymentType.Split, () =>
        {
            RuleFor(x => x.SplitCashAmount)
                .NotNull().GreaterThanOrEqualTo(0)
                .WithMessage("Pour un paiement partagé, le montant Espèces est requis.");
            RuleFor(x => x.SplitCardAmount)
                .NotNull().GreaterThanOrEqualTo(0)
                .WithMessage("Pour un paiement partagé, le montant Carte est requis.");
        });

        // Credit (= customer tab) requires a customer to be attached.
        When(x => x.PaymentMethod == PaymentType.Credit, () =>
        {
            RuleFor(x => x.CustomerId)
                .NotNull().GreaterThan(0)
                .WithMessage("Un client doit être sélectionné pour un paiement à crédit.");
        });
    }
}

public sealed class SaleDetailForCreateDtoValidator : AbstractValidator<SaleDetailForCreateDto>
{
    public SaleDetailForCreateDtoValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("La quantité doit être supérieure à 0.");
    }
}
