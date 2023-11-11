using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Tag;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.Items
{
    public interface ITagService : IAbstractService<TagVm, ITagRepository, Tag>
    {
        int AddTag(TagVm model);
        TagDetailsVm GetTagDetails(int id);
        TagVm GetTagById(int id);
        void UpdateTag(TagVm model);
        IEnumerable<TagVm> GetTags(Expression<Func<Tag, bool>> expression);
        ListForTagsVm GetTags(int pageSize, int pageNo, string searchString);
        bool TagExists(int id);
        void DeleteTag(int id);
    }
}
