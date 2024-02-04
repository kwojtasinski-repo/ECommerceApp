using AutoMapper;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.Exceptions;
using ECommerceApp.Application.ViewModels.Tag;
using ECommerceApp.Domain.Interface;
using ECommerceApp.Domain.Model;
using System.Collections.Generic;

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

        public bool DeleteTag(int id)
        {
            return _tagRepository.DeleteTag(id);
        }

        public TagDto GetTagById(int id)
        {
            var tag = _tagRepository.GetTagById(id);
            return _mapper.Map<TagDto>(tag);
        }

        public TagDetailsVm GetTagDetails(int id)
        {
            return _mapper.Map<TagDetailsVm>(_tagRepository.GetTagDetailsById(id));
        }

        public IEnumerable<TagDto> GetTags()
        {
            return _mapper.Map<List<TagDto>>(_tagRepository.GetAllTags());
        }

        public ListForTagsVm GetTags(int pageSize, int pageNo, string searchString)
        {
            var tags = _mapper.Map<List<TagDto>>(_tagRepository.GetAllTags(pageSize, pageNo, searchString));

            var tagsList = new ListForTagsVm()
            {
                PageSize = pageSize,
                CurrentPage = pageNo,
                SearchString = searchString,
                Tags = tags,
                Count = _tagRepository.GetCountBySearchString(searchString)
            };

            return tagsList;
        }

        public bool TagExists(int id)
        {
            return _tagRepository.ExistsById(id);
        }

        public bool UpdateTag(TagDto model)
        {
            if (model is null)
            {
                throw new BusinessException($"{typeof(TagDto).Name} cannot be null");
            }
            if (!TagExists(model.Id))
            {
                return false;
            }

            var tag = _mapper.Map<Tag>(model);
            _tagRepository.UpdateTag(tag);
            return true;
        }
    }
}
