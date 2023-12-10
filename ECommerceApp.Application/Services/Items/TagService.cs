using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.Tag;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace ECommerceApp.Application.Services.Items
{
    public class TagService : ITagService
    {
        private readonly IMapper _mapper;
        private readonly ITagRepository _tagRepository;

        public TagService(ITagRepository tagRepository, IMapper mapper)
        {
            _mapper = mapper;
            _tagRepository = tagRepository;
        }

        public int AddTag(TagDto model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(TagDto).Name} cannot be null");
            }

            if (model.Id != 0)
            {
                throw new BusinessException("When adding object Id should be equals 0");
            }

            var tag = _mapper.Map<Tag>(model);
            var id = _tagRepository.AddTag(tag);
            return id;
        }

        public void DeleteTag(int id)
        {
            _tagRepository.DeleteTag(id);
        }

        public TagDto GetTagById(int id)
        {
            var tag = _tagRepository.GetById(id);
            return _mapper.Map<TagDto>(tag);
        }

        public TagDetailsVm GetTagDetails(int id)
        {
            var tag = _tagRepository.GetAll().Include(it => it.ItemTags)
                .ThenInclude(i => i.Item)
                .Where(t => t.Id == id).FirstOrDefault();
            var tagVm = _mapper.Map<TagDetailsVm>(tag);
            return tagVm;
        }

        public IEnumerable<TagDto> GetTags(Expression<Func<Tag, bool>> expression)
        {
            var tags = _tagRepository.GetAll().Where(expression)
               .ProjectTo<TagDto>(_mapper.ConfigurationProvider);
            var tagsToShow = tags.ToList();

            return tagsToShow;
        }

        public ListForTagsVm GetTags(int pageSize, int pageNo, string searchString)
        {
            var tags = _tagRepository.GetAllTags().Where(it => it.Name.StartsWith(searchString))
                .ProjectTo<TagDto>(_mapper.ConfigurationProvider)
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
            var tag = _tagRepository.GetById(id);
            var exists = tag != null;

            if (exists)
            {
                _tagRepository.DetachEntity(tag);
            }

            return exists;
        }

        public void UpdateTag(TagDto model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(TagDto).Name} cannot be null");
            }

            var tag = _mapper.Map<Tag>(model);
            if (tag != null)
            {
                _tagRepository.UpdateTag(tag);
            }
        }
    }
}
