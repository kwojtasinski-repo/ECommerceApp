using AutoMapper;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Supporting.Currencies.DTOs;
using ECommerceApp.Application.Supporting.Currencies.ViewModels;
using ECommerceApp.Domain.Supporting.Currencies;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ECommerceApp.Application.Supporting.Currencies.Services
{
    internal sealed class CurrencyService : ICurrencyService
    {
        private readonly ICurrencyRepository _repo;
        private readonly IMapper _mapper;

        public CurrencyService(ICurrencyRepository repo, IMapper mapper)
        {
            _repo = repo;
            _mapper = mapper;
        }

        public async Task<int> AddAsync(CreateCurrencyDto dto)
        {
            if (dto is null)
                throw new BusinessException($"{nameof(CreateCurrencyDto)} cannot be null");

            var currency = Currency.Create(dto.Code, dto.Description);
            var id = await _repo.AddAsync(currency);
            return id.Value;
        }

        public async Task<bool> UpdateAsync(UpdateCurrencyDto dto)
        {
            if (dto is null)
                throw new BusinessException($"{nameof(UpdateCurrencyDto)} cannot be null");

            var currency = await _repo.GetByIdAsync(new CurrencyId(dto.Id));
            if (currency is null)
                return false;

            currency.Update(dto.Code, dto.Description);
            await _repo.UpdateAsync(currency);
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repo.DeleteAsync(new CurrencyId(id));
        }

        public async Task<CurrencyVm> GetByIdAsync(int id)
        {
            var currency = await _repo.GetByIdAsync(new CurrencyId(id));
            return currency is null ? null : _mapper.Map<CurrencyVm>(currency);
        }

        public async Task<List<CurrencyVm>> GetAllAsync()
        {
            var currencies = await _repo.GetAllAsync();
            return _mapper.Map<List<CurrencyVm>>(currencies);
        }

        public async Task<CurrencyListVm> GetAllAsync(int pageSize, int pageNo, string searchString)
        {
            var currencies = await _repo.GetAllAsync(pageSize, pageNo, searchString);
            var count = await _repo.CountBySearchStringAsync(searchString);

            return new CurrencyListVm
            {
                Currencies = _mapper.Map<List<CurrencyVm>>(currencies),
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Count = count
            };
        }
    }
}
