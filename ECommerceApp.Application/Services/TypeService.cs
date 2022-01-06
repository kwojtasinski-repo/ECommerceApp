using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Type;
using ECommerceApp.Domain.Interface;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Services
{
    public class TypeService : AbstractService<TypeVm, ITypeRepository, Domain.Model.Type>, ITypeService
    {
        public TypeService(ITypeRepository typeRepository, IMapper mapper) : base(typeRepository, mapper)
        { }

        public int AddType(TypeVm model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(TypeVm).Name} cannot be null");
            }

            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var type = _mapper.Map<ECommerceApp.Domain.Model.Type>(model);
            var id = _repo.AddType(type);
            return id;
        }

        public void DeleteType(int id)
        {
            _repo.DeleteType(id);
        }

        public TypeVm GetTypeById(int id)
        {
            var type = Get(id);
            return type;
        }

        public TypeDetailsVm GetTypeDetails(int id)
        {
            var type = _repo.GetAll().Include(t => t.Items).Where(t => t.Id == id).FirstOrDefault();
            var typeVm = _mapper.Map<TypeDetailsVm>(type);
            return typeVm;
        }

        public IEnumerable<TypeVm> GetTypes(Expression<Func<Domain.Model.Type, bool>> expression)
        {
            var types = _repo.GetAll().Where(expression)
               .ProjectTo<TypeVm>(_mapper.ConfigurationProvider);
            var typesToShow = types.ToList();

            return typesToShow;
        }

        public ListForTypeVm GetTypes(int pageSize, int pageNo, string searchString)
        {
            var types = _repo.GetAllTypes().Where(it => it.Name.StartsWith(searchString))
                .ProjectTo<TypeVm>(_mapper.ConfigurationProvider)
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
            var type = _repo.GetById(id);
            var exists = type != null;
            
            if (exists)
            {
                _repo.DetachEntity(type);
            }

            return exists;
        }

        public void UpdateType(TypeVm model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(TypeVm).Name} cannot be null");
            }

            var type = _mapper.Map<ECommerceApp.Domain.Model.Type>(model);
            if (type != null)
            {
                _repo.UpdateType(type);
            }
        }
    }
}
