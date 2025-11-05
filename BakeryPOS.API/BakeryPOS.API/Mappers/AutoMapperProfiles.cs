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
            CreateMap<Product, ProductDto>();

            // We also need mappings for our Create and Update DTOs back to the Product entity
            CreateMap<ProductForCreateDto, Product>();
            CreateMap<ProductForUpdateDto, Product>();

            // --- Customer Mappings ---
            CreateMap<Customer, CustomerDto>();
            CreateMap<CustomerForCreateDto, Customer>();
        }
    }
}