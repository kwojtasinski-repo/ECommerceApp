using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Interfaces
{
    public interface ITagService : IAbstractService<TagVm, ITagRepository, Tag>
    {
        int AddTag(TagVm model);
        TagDetailsVm GetTagDetails(int id);
        TagVm GetTagById(int id);
        void UpdateTag(TagVm model);
        IEnumerable<TagVm> GetTags(Expression<Func<Domain.Model.Tag, bool>> expression);
        ListForTagsVm GetTags(int pageSize, int pageNo, string searchString);
        bool TagExists(int id);
        void DeleteTag(int id);
    }
}
