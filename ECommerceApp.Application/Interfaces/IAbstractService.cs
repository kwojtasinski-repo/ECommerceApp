namespace ECommerceApp.Application.Interfaces
{
    public interface IAbstractService<T, U, E>
    {
        T Get(int id);
        void Update(T vm);
        void Delete(T vm);
        bool Delete(int id);
        int Add(T vm);
    }
}
