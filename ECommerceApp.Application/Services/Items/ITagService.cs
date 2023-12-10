using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Tag;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.Items
{
    public interface ITagService
    {
        int AddTag(TagDto model);
        TagDetailsVm GetTagDetails(int id);
        TagDto GetTagById(int id);
        void UpdateTag(TagDto model);
        IEnumerable<TagDto> GetTags(Expression<Func<Tag, bool>> expression);
        ListForTagsVm GetTags(int pageSize, int pageNo, string searchString);
        bool TagExists(int id);
        void DeleteTag(int id);
    }
}
