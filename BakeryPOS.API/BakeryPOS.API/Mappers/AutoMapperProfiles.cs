using AutoMapper;
using BakeryPOS.API.Controllers;
using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.DTOs;

namespace BakeryPOS.API.Mappers
{
    public class AutoMapperProfiles : Profile
    {
        public AutoMapperProfiles()
        {
            // This line tells AutoMapper:
            // "You can map from a Product object to a ProductDto object."
            // Because the property names are identical, AutoMapper will
            // handle all the property mapping automatically.
            CreateMap<Product, ProductDto>()
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name));

            // We also need mappings for our Create and Update DTOs back to the Product entity
            CreateMap<ProductForCreateDto, Product>();
            CreateMap<ProductForUpdateDto, Product>();

            // --- Customer Mappings ---
            CreateMap<Customer, CustomerDto>();
            CreateMap<CustomerForCreateDto, Customer>();

            CreateMap<User, UserDetailDto>();
            CreateMap<UserForUpdateDto, User>();

            CreateMap<Customer, CustomerDto>();
            CreateMap<CustomerForCreateDto, Customer>();
            CreateMap<CustomerForUpdateDto, Customer>();

            CreateMap<Sale, SaleListDto>()
                // For CashierName, we tell AutoMapper to look inside the related User object
                .ForMember(dest => dest.CashierName, opt => opt.MapFrom(src => src.User.FullName))
                // For CustomerName, we do the same, but handle the case where the customer might be null
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer != null ? src.Customer.Name : null));

            // Mapping for a single line item in a sale's details
            CreateMap<SaleDetail, SaleItemDto>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name));

            // Mapping for the full, detailed view of a single sale
            CreateMap<Sale, SaleDetailDto>()
                .ForMember(dest => dest.CashierName, opt => opt.MapFrom(src => src.User.FullName))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer != null ? src.Customer.Name : null))
                // This tells AutoMapper to map the Sale.SaleDetails collection to the SaleDetailDto.Items collection
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.SaleDetails))
                // We also need to map the enum to its string representation
                .ForMember(dest => dest.PaymentMethod, opt => opt.MapFrom(src => src.PaymentMethod.ToString()));

            CreateMap<Category, CategoryDto>();
            CreateMap<CategoryForCreateDto, Category>();

            // Expense Category Mappings
            CreateMap<ExpenseCategory, ExpenseCategoryDto>();
            CreateMap<ExpenseCategoryForCreateDto, ExpenseCategory>();

            // Expense Mappings
            CreateMap<Expense, ExpenseDto>()
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name))
                .ForMember(dest => dest.RecordedByUserName, opt => opt.MapFrom(src => src.User.FullName));

            CreateMap<ExpenseForCreateDto, Expense>();
        }
    }
}