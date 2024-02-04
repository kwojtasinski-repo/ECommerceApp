using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.Type;
using ECommerceApp.Domain.Interface;
using System.Collections.Generic;

namespace ECommerceApp.Application.Services.Items
{
    public class TypeService : ITypeService
    {
        private readonly IMapper _mapper;
        private readonly ITypeRepository _typeRepository;

        public TypeService(ITypeRepository typeRepository, IMapper mapper)
        {
            _mapper = mapper;
            _typeRepository = typeRepository;
        }

        public int AddType(TypeDto model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(TypeDto).Name} cannot be null");
            }

            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var type = _mapper.Map<Domain.Model.Type>(model);
            var id = _typeRepository.AddType(type);
            return id;
        }

        public bool DeleteType(int id)
        {
            return _typeRepository.DeleteType(id);
        }

        public TypeDto GetTypeById(int id)
        {
            var type = _typeRepository.GetTypeById(id);
            return _mapper.Map<TypeDto>(type);
        }

        public TypeDetailsVm GetTypeDetails(int id)
        {
            return _mapper.Map<TypeDetailsVm>(_typeRepository.GetTypeDetailsById(id));
        }

        public IEnumerable<TypeDto> GetTypes()
        {
            return _mapper.Map<List<TypeDto>>(_typeRepository.GetAllTypes());
        }

        public ListForTypeVm GetTypes(int pageSize, int pageNo, string searchString)
        {
            var types = _mapper.Map<List<TypeDto>>(_typeRepository.GetAllTypes(pageSize, pageNo, searchString));

            var typesList = new ListForTypeVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Types = types,
                Count = _typeRepository.GetCountBySearchString(searchString)
            };

            return typesList;
        }

        public bool TypeExists(int id)
        {
            return _typeRepository.ExistsById(id);
        }

        public bool UpdateType(TypeDto model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(TypeDto).Name} cannot be null");
            }

            if (!TypeExists(model.Id))
            {
                return false;
            }

            var type = _mapper.Map<Domain.Model.Type>(model);
            _typeRepository.UpdateType(type);
            return true;
        }
    }
}
