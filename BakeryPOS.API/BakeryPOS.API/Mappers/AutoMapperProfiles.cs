using AutoMapper;
using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Enums;
using BakeryPOS.API.DTOs;

namespace BakeryPOS.API.Mappers
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
            CreateMap<Sale, SaleListDto>()
                .ForMember(dest => dest.CashierName, opt => opt.MapFrom(src => src.User.FullName))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer != null ? src.Customer.Name : null));

            CreateMap<SaleDetail, SaleItemDto>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name));

            CreateMap<Sale, SaleDetailDto>()
                .ForMember(dest => dest.CashierName, opt => opt.MapFrom(src => src.User.FullName))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer != null ? src.Customer.Name : null))
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.SaleDetails))
                .ForMember(dest => dest.PaymentMethod, opt => opt.MapFrom(src => src.PaymentMethod.ToString()));

            // --- Expense Mappings ---
            CreateMap<ExpenseCategory, ExpenseCategoryDto>();
            CreateMap<ExpenseCategoryForCreateDto, ExpenseCategory>();

            CreateMap<Expense, ExpenseDto>()
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name))
                .ForMember(dest => dest.RecordedByUserName, opt => opt.MapFrom(src => src.User.FullName));

            CreateMap<ExpenseForCreateDto, Expense>();
        }
    }
}