using AutoMapper;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;

namespace ECommerceApp.Application.Abstracts
{
    public abstract class AbstractService<T, U, E> : IAbstractService<T, U, E> 
        where T : BaseVm 
        where E : BaseEntity 
        where U : IGenericRepository<E>
    {
        protected readonly U _repo;
        protected readonly IMapper _mapper;

        public AbstractService(U repo)
        {
            _repo = repo;
        }

        public AbstractService(U repo, IMapper mapper)
        {
            _repo = repo;
            _mapper = mapper;
        }

        public virtual int Add(T vm)
        {
            if (vm.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var entity = _mapper.Map<E>(vm);
            var id = _repo.Add(entity);
            return id;
        }

        public virtual void Delete(T vm)
        {
            var entity = _mapper.Map<E>(vm);
            _repo.Delete(entity);
        }

        public virtual void Delete(int id)
        {
            _repo.Delete(id);
        }

        public virtual T Get(int id)
        {
            var entity = _repo.GetById(id);
            var vm = _mapper.Map<T>(entity);
            return vm;
        }

        public virtual void Update(T vm)
        {
            var entity = _mapper.Map<E>(vm);
            _repo.Update(entity);
        }
    }
}
