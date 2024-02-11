namespace ECommerceApp.Application.Services.Items
{
    internal interface IItemHandler
    {
        void HandleItemsChangesOnOrder(Domain.Model.Order orderBeforeChange, Domain.Model.Order orderAfterChange);
    }
}
