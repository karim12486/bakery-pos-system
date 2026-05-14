using AutoMapper;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.DTOs;

namespace Nizam.Api.Mappers
{
    public class AutoMapperProfiles : Profile
    {
        public AutoMapperProfiles()
        {
            // --- Product Mappings ---
            CreateMap<Product, ProductDto>()
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name));
            CreateMap<ProductForCreateDto, Product>();
            CreateMap<ProductForUpdateDto, Product>();

            // --- Category Mappings ---
            CreateMap<Category, CategoryDto>();
            CreateMap<CategoryForCreateDto, Category>();

            // --- User Mappings ---
            CreateMap<User, UserDetailDto>()
                // Cast Permissions Enum to int for the frontend
                .ForMember(dest => dest.Permissions, opt => opt.MapFrom(src => (int)src.Permissions))
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src =>
                    !string.IsNullOrEmpty(src.Role)
                        ? src.Role
                        : (src.Permissions.HasFlag(UserPermissions.Admin) ? "Admin" : "Cashier")));
            // Role is mapped automatically because the names match in DB and DTO
            CreateMap<UserForUpdateDto, User>();
            CreateMap<UserForCreationDto, User>();

            // --- Customer Mappings ---
            CreateMap<Customer, CustomerDto>();
            CreateMap<CustomerForCreateDto, Customer>();
            CreateMap<CustomerForUpdateDto, Customer>();

            // Detailed Customer Profile with Calculations
            CreateMap<Customer, CustomerDetailDto>()
                .ForMember(dest => dest.TotalSpent, opt => opt.MapFrom(src => src.Sales.Sum(s => s.FinalAmount)))
                .ForMember(dest => dest.TotalDiscount, opt => opt.MapFrom(src => src.Sales.Sum(s => s.DiscountAmount)))
                // Calculate Outstanding Balance (convert negative balance to positive debt)
                .ForMember(dest => dest.OutstandingBalance, opt => opt.MapFrom(src => src.CurrentBalance < 0 ? src.CurrentBalance * -1 : 0));

            // --- Sales Mappings ---
            // Navigation properties (User, Customer, Product) can be null if a query forgot
            // an Include, or if a cross-tenant reference somehow exists. Defensive null-coalesce
            // on every nav-property access — better to render "Unknown" than throw NRE inside
            // AutoMapper (which surfaces as an opaque 500).
            CreateMap<Sale, SaleListDto>()
                .ForMember(dest => dest.CashierName, opt => opt.MapFrom(src => src.User != null ? src.User.FullName : "Unknown"))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer != null ? src.Customer.Name : null));

            CreateMap<SaleDetail, SaleItemDto>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : "Unknown"));

            CreateMap<Sale, SaleDetailDto>()
                .ForMember(dest => dest.CashierName, opt => opt.MapFrom(src => src.User != null ? src.User.FullName : "Unknown"))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer != null ? src.Customer.Name : null))
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.SaleDetails))
                .ForMember(dest => dest.PaymentMethod, opt => opt.MapFrom(src => src.PaymentMethod.ToString()));

            // --- Expense Mappings ---
            CreateMap<ExpenseCategory, ExpenseCategoryDto>();
            CreateMap<ExpenseCategoryForCreateDto, ExpenseCategory>();
            CreateMap<ExpenseCategoryForUpdateDto, ExpenseCategory>();
            CreateMap<Expense, ExpenseDto>()
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : "Unknown"))
                .ForMember(dest => dest.RecordedByUserName, opt => opt.MapFrom(src => src.User != null ? src.User.FullName : "Unknown"));

            CreateMap<ExpenseForCreateDto, Expense>();
        }
    }
}