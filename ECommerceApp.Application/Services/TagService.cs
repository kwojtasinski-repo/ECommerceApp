using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.Abstracts;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.Interfaces;
using ECommerceApp.Application.ViewModels.Item;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ECommerceApp.Application.Services
{
    public class TagService : AbstractService<TagVm, ITagRepository, Tag>, ITagService
    {
        public TagService(ITagRepository tagRepository, IMapper mapper) : base(tagRepository, mapper)
        { }

        public int AddTag(TagVm model)
        {
            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var tag = _mapper.Map<Tag>(model);
            var id = _repo.AddTag(tag);
            return id;
        }

        public void DeleteTag(int id)
        {
            _repo.DeleteTag(id);
        }

        public TagVm GetTagById(int id)
        {
            var tag = Get(id);
            return tag;
        }

        public TagDetailsVm GetTagDetails(int id)
        {
            var tag = _repo.GetAll().Include(it => it.ItemTags).ThenInclude(i => i.Item).Where(t => t.Id == id).FirstOrDefault();
            var tagVm = _mapper.Map<TagDetailsVm>(tag);
            return tagVm;
        }

        public IEnumerable<TagVm> GetTags(Expression<Func<Tag, bool>> expression)
        {
            var tags = _repo.GetAll().Where(expression)
               .ProjectTo<TagVm>(_mapper.ConfigurationProvider);
            var tagsToShow = tags.ToList();

            return tagsToShow;
        }

        public ListForTagsVm GetTags(int pageSize, int pageNo, string searchString)
        {
            var tags = _repo.GetAllTags().Where(it => it.Name.StartsWith(searchString))
                .ProjectTo<TagVm>(_mapper.ConfigurationProvider)
                .ToList();
            var tagsToShow = tags.Skip(pageSize * (pageNo - 1)).Take(pageSize).ToList();

            var tagsList = new ListForTagsVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Tags = tagsToShow,
                Count = tags.Count
            };

            return tagsList;
        }

        public bool TagExists(int id)
        {
            var tag = _repo.GetById(id);
            var exists = tag != null;

            if (exists)
            {
                _repo.DetachEntity(tag);
            }

            return exists;
        }

        public void UpdateTag(TagVm model)
        {
            var tag = _mapper.Map<Tag>(model);
            if (tag != null)
            {
                _repo.UpdateTag(tag);
            }
        }
    }
}
