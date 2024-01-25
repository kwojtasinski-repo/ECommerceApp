using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.Type;
using ECommerceApp.Domain.Interface;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

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

        public void DeleteType(int id)
        {
            _typeRepository.DeleteType(id);
        }

        public TypeDto GetTypeById(int id)
        {
            var type = _typeRepository.GetTypeById(id);
            return _mapper.Map<TypeDto>(type);
        }

        public TypeDetailsVm GetTypeDetails(int id)
        {
            var type = _typeRepository.GetAllTypes().Include(t => t.Items).Where(t => t.Id == id).FirstOrDefault();
            var typeVm = _mapper.Map<TypeDetailsVm>(type);
            return typeVm;
        }

        public IEnumerable<TypeDto> GetTypes(Expression<Func<Domain.Model.Type, bool>> expression)
        {
            var types = _typeRepository.GetAllTypes().Where(expression)
               .ProjectTo<TypeDto>(_mapper.ConfigurationProvider);
            var typesToShow = types.ToList();

            return typesToShow;
        }

        public ListForTypeVm GetTypes(int pageSize, int pageNo, string searchString)
        {
            var types = _typeRepository.GetAllTypes().Where(it => it.Name.StartsWith(searchString))
                .ProjectTo<TypeDto>(_mapper.ConfigurationProvider)
                .ToList();
            var typesToShow = types.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var typesList = new ListForTypeVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Types = typesToShow,
                Count = types.Count
            };

            return typesList;
        }

        public bool TypeExists(int id)
        {
            return _typeRepository.ExistsById(id);
        }

        public void UpdateType(TypeDto model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(TypeDto).Name} cannot be null");
            }

            var type = _mapper.Map<Domain.Model.Type>(model);
            if (type != null)
            {
                _typeRepository.UpdateType(type);
            }
        }
    }
}
