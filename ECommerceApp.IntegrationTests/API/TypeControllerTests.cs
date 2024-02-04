using ECommerceApp.API;
using ECommerceApp.Application.DTO;
using ECommerceApp.Application.ViewModels.Type;
using ECommerceApp.IntegrationTests.Common;
using Flurl.Http;
using Newtonsoft.Json;
using Shouldly;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.API
{
    public class TypeControllerTests : IClassFixture<CustomWebApplicationFactory<Startup>>
    {
        private readonly CustomWebApplicationFactory<Startup> _factory;

        public TypeControllerTests(CustomWebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task given_valid_id_should_return_type()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 1;

            var response = await client.Request($"api/types/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();

            var tag = JsonConvert.DeserializeObject<TypeDto>(await response.ResponseMessage.Content.ReadAsStringAsync());
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            tag.ShouldNotBeNull();
            tag.Id.ShouldBe(id);
        }

        [Fact]
        public async Task given_invalid_id_should_return_status_code_not_found()
        {
            var client = await _factory.GetAuthenticatedClient();
            var id = 13453;

            var response = await client.Request($"api/types/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();

            response.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_valid_type_should_add()
        {
            var client = await _factory.GetAuthenticatedClient();
            var tag = new TypeDto { Id = 0, Name = "Type2" };

            var response = await client.Request("api/types")
                .AllowAnyHttpStatus()
                .PostJsonAsync(tag);

            var id = JsonConvert.DeserializeObject<int>(await response.ResponseMessage.Content.ReadAsStringAsync());
            response.StatusCode.ShouldBe((int)HttpStatusCode.OK);
            id.ShouldBeGreaterThan(1);
        }

        [Fact]
        public async Task given_invalid_type_should_return_status_code_conflict()
        {
            var client = await _factory.GetAuthenticatedClient();
            var tag = new TypeDto { Id = 235 };

            var response = await client.Request("api/types")
                .AllowAnyHttpStatus()
                .PostJsonAsync(tag);

            response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task given_valid_type_should_update()
        {
            var client = await _factory.GetAuthenticatedClient();
            var tag = new TypeDto { Id = 0, Name = "Type2" };
            var id = await client.Request("api/types")
                .AllowAnyHttpStatus()
                .PostJsonAsync(tag)
                .ReceiveJson<int>();
            tag.Id = id;
            var name = "Type25";
            tag.Name = name;

            var response = await client.Request($"api/types/{id}")
                .AllowAnyHttpStatus()
                .PutJsonAsync(tag);

            var tagUpdated = await client.Request($"api/types/{id}")
                .AllowAnyHttpStatus()
                .GetJsonAsync<TypeDto>();
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            tagUpdated.ShouldNotBeNull();
            tagUpdated.Name.ShouldBe(name);
        }

        [Fact]
        public async Task given_invalid_type_when_update_should_return_status_code_not_found()
        {
            var client = await _factory.GetAuthenticatedClient();
            var tag = new TypeDto { Id = 234235 };

            var response = await client.Request($"api/types/{tag.Id}")
                .AllowAnyHttpStatus()
                .PutJsonAsync(tag);

            response.StatusCode.ShouldBe((int) HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task given_valid_type_should_delete()
        {
            var client = await _factory.GetAuthenticatedClient();
            var type = new TypeDto { Id = 0, Name = "Type2" };
            var id = await client.Request("api/types")
                .AllowAnyHttpStatus()
                .PostJsonAsync(type)
                .ReceiveJson<int>();

            var response = await client.Request($"api/types/{id}")
                .AllowAnyHttpStatus()
                .DeleteAsync();

            var typeDeleted = await client.Request($"api/types/{id}")
                .AllowAnyHttpStatus()
                .GetAsync();
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            typeDeleted.StatusCode.ShouldBe((int) HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task given_types_in_db_should_return_types()
        {
            var client = await _factory.GetAuthenticatedClient();
            var response = await client.Request($"api/types")
                .AllowAnyHttpStatus()
                .GetAsync();

            var types = JsonConvert.DeserializeObject<List<TypeVm>>(await response.ResponseMessage.Content.ReadAsStringAsync());
            response.StatusCode.ShouldBe((int) HttpStatusCode.OK);
            types.Count.ShouldBeGreaterThan(0);
        }
    }
}
