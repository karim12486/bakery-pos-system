using BakeryPOS.API.DTOs;
using FluentValidation;

namespace BakeryPOS.API.DTOs.Validators;

// One file for the medium-cost write DTOs. Per-DTO files welcome as the codebase grows.

public sealed class UserForLoginDtoValidator : AbstractValidator<UserForLoginDto>
{
    public UserForLoginDtoValidator()
    {
        RuleFor(x => x.Username).NotEmpty().Length(3, 50);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}

public sealed class UserForCreationDtoValidator : AbstractValidator<UserForCreationDto>
{
    public UserForCreationDtoValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty()
            .Length(3, 50)
            .Matches("^[a-zA-Z0-9._-]+$")
            .WithMessage("Le nom d'utilisateur ne peut contenir que des lettres, chiffres, '.', '_' ou '-'.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(100)
            .Matches(@"^(?=.*[A-Za-z])(?=.*\d).{8,100}$")
            .WithMessage("Le mot de passe doit contenir au moins une lettre et un chiffre.");

        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
    }
}

public sealed class CustomerForCreateDtoValidator : AbstractValidator<CustomerForCreateDto>
{
    public CustomerForCreateDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber).MaximumLength(30);
        RuleFor(x => x.DiscountPercentage).InclusiveBetween(0, 100);
    }
}

public sealed class CustomerPaymentDtoValidator : AbstractValidator<CustomerPaymentDto>
{
    public CustomerPaymentDtoValidator()
    {
        RuleFor(x => x.AmountPaid)
            .GreaterThan(0).WithMessage("Le montant du paiement doit être supérieur à 0.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class ProductForCreateDtoValidator : AbstractValidator<ProductForCreateDto>
{
    public ProductForCreateDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Barcode).MaximumLength(50);
        RuleFor(x => x.CostPrice).InclusiveBetween(0m, 10000m);
        RuleFor(x => x.Price).GreaterThan(0).LessThanOrEqualTo(10000);
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CategoryId).GreaterThan(0);
    }
}

public sealed class ProductForUpdateDtoValidator : AbstractValidator<ProductForUpdateDto>
{
    public ProductForUpdateDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Barcode).MaximumLength(50);
        RuleFor(x => x.CostPrice).InclusiveBetween(0m, 10000m);
        RuleFor(x => x.Price).GreaterThan(0).LessThanOrEqualTo(10000);
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CategoryId).GreaterThan(0);
    }
}

public sealed class ExpenseForCreateDtoValidator : AbstractValidator<ExpenseForCreateDto>
{
    public ExpenseForCreateDtoValidator()
    {
        RuleFor(x => x.Date).LessThanOrEqualTo(_ => DateTime.UtcNow.AddDays(1))
            .WithMessage("La date ne peut pas être dans le futur.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.CategoryId).GreaterThan(0);
    }
}
