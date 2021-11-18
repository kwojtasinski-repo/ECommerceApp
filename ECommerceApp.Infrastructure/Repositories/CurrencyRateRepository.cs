using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using ECommerceApp.Infrastructure.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ECommerceApp.Infrastructure.Repositories
{
    public class CurrencyRateRepository : GenericRepository<CurrencyRate>, ICurrencyRateRepository
    {
        public CurrencyRateRepository(Context context) : base(context)
        {
        }

        public override int Add(CurrencyRate entity)
        {
            var type = entity.GetType();
            throw new InfrastructureException($"For entity '{type.Name}' using method Add is not allowed");
        }

        public override Task<int> AddAsync(CurrencyRate entity)
        {
            var type = entity.GetType();
            throw new InfrastructureException($"For entity '{type.Name}' using method AddAsync is not allowed");
        }

        public override List<int> AddRange(List<CurrencyRate> entities)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method AddRange is not allowed");
        }

        public override Task<List<int>> AddRangeAsync(List<CurrencyRate> entities)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method AddRangeAsync is not allowed");
        }

        public override void Delete(CurrencyRate entity)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method Delete is not allowed");
        }

        public override void Delete(int id)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method Delete is not allowed");
        }

        public override Task DeleteAsync(CurrencyRate entity)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method DeleteAsync is not allowed");
        }

        public override Task DeleteAsync(int id)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method DeleteAsync is not allowed");
        }

        public override CurrencyRate GetById(int id)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method GetById is not allowed");
        }

        public override Task<CurrencyRate> GetByIdAsync(int id)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method GetByIdAsync is not allowed");
        }

        public override void Update(CurrencyRate entity)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method Update is not allowed");
        }

        public override Task UpdateAsync(CurrencyRate entity)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method UpdateAsync is not allowed");
        }

        public override void UpdateRange(IEnumerable<CurrencyRate> entities)
        {
            throw new InfrastructureException("For entity 'CurrencyRate' using method UpdateRange is not allowed");
        }

        public decimal GetRateForDay(DateTime dateTime)
        {
            throw new NotImplementedException();
        }
    }
}
