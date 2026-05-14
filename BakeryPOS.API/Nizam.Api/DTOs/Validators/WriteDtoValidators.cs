// FluentValidation validators in this project are reserved for DTOs that have CROSS-FIELD
// rules or DB-dependent invariants — like SaleForCreateDto (split-payment requires both
// halves; credit-payment requires a customer; etc.). Those are wired up by services that
// explicitly inject IValidator<T> and call ValidateAndThrowAsync.
//
// Simple shape/format rules (Required, StringLength, RegularExpression, Range) stay on the
// DTO via DataAnnotations and are auto-validated by [ApiController]. We don't double-validate
// the same DTO with both systems — that's the source of the H2 audit finding.
//
// This file intentionally has no per-feature validators today. Add one here ONLY when a DTO
// needs rich rules that DataAnnotations can't express and the call site explicitly injects
// IValidator<T>. See SaleForCreateDtoValidator for the canonical pattern.
