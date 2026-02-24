using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Catalog.Products.Events;
using ECommerceApp.Domain.Shared;
using FluentAssertions;
using System.Linq;
using Xunit;

namespace ECommerceApp.UnitTests.Catalog.Products
{
    public class ProductAggregateTests
    {
        [Fact]
        public void Create_ValidParameters_ShouldCreateDraftProduct()
        {
            var (product, @event) = Product.Create("Test Product", 10.00m, 5, "Description", 1);

            product.Should().NotBeNull();
            product.Name.Value.Should().Be("Test Product");
            product.Cost.Amount.Should().Be(10.00m);
            product.Quantity.Value.Should().Be(5);
            product.Description.Value.Should().Be("Description");
            product.Status.Should().Be(ProductStatus.Draft);
            product.CategoryId.Value.Should().Be(1);
            @event.Should().NotBeNull();
            @event.Name.Should().Be("Test Product");
        }

        [Fact]
        public void Create_NegativeQuantity_ShouldThrowDomainException()
        {
            var act = () => Product.Create("Test", 10m, -1, "Desc", 1);

            act.Should().Throw<DomainException>().WithMessage("*negative*");
        }

        [Fact]
        public void Create_ZeroCost_ShouldThrowDomainException()
        {
            var act = () => Product.Create("Test", 0m, 1, "Desc", 1);

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        [Fact]
        public void Create_InvalidCategoryId_ShouldThrowDomainException()
        {
            var act = () => Product.Create("Test", 10m, 1, "Desc", 0);

            act.Should().Throw<DomainException>().WithMessage("*CategoryId*positive*");
        }

        [Fact]
        public void Publish_DraftProduct_ShouldReturnProductPublishedEvent()
        {
            var (product, _) = Product.Create("Test", 10m, 1, "Desc", 1);

            var @event = product.Publish();

            product.Status.Should().Be(ProductStatus.Published);
            @event.Should().NotBeNull();
            @event.Should().BeOfType<ProductPublished>();
        }

        [Fact]
        public void Publish_AlreadyPublishedProduct_ShouldThrowDomainException()
        {
            var (product, _) = Product.Create("Test", 10m, 1, "Desc", 1);
            product.Publish();

            var act = () => product.Publish();

            act.Should().Throw<DomainException>().WithMessage("*already published*");
        }

        [Fact]
        public void Unpublish_PublishedProduct_ShouldReturnProductUnpublishedEvent()
        {
            var (product, _) = Product.Create("Test", 10m, 1, "Desc", 1);
            product.Publish();

            var @event = product.Unpublish();

            product.Status.Should().Be(ProductStatus.Unpublished);
            @event.Should().NotBeNull();
            @event.Should().BeOfType<ProductUnpublished>();
        }

        [Fact]
        public void Unpublish_DraftProduct_ShouldThrowDomainException()
        {
            var (product, _) = Product.Create("Test", 10m, 1, "Desc", 1);

            var act = () => product.Unpublish();

            act.Should().Throw<DomainException>().WithMessage("*Only published*");
        }

        [Fact]
        public void DecreaseQuantity_ValidAmount_ShouldDecrease()
        {
            var (product, _) = Product.Create("Test", 10m, 10, "Desc", 1);

            product.DecreaseQuantity(3);

            product.Quantity.Value.Should().Be(7);
        }

        [Fact]
        public void DecreaseQuantity_MoreThanAvailable_ShouldThrowDomainException()
        {
            var (product, _) = Product.Create("Test", 10m, 2, "Desc", 1);

            var act = () => product.DecreaseQuantity(5);

            act.Should().Throw<DomainException>().WithMessage("*Cannot decrease*");
        }

        [Fact]
        public void DecreaseQuantity_ZeroAmount_ShouldThrowDomainException()
        {
            var (product, _) = Product.Create("Test", 10m, 10, "Desc", 1);

            var act = () => product.DecreaseQuantity(0);

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        [Fact]
        public void IncreaseQuantity_ValidAmount_ShouldIncrease()
        {
            var (product, _) = Product.Create("Test", 10m, 5, "Desc", 1);

            product.IncreaseQuantity(3);

            product.Quantity.Value.Should().Be(8);
        }

        [Fact]
        public void IncreaseQuantity_ZeroAmount_ShouldThrowDomainException()
        {
            var (product, _) = Product.Create("Test", 10m, 5, "Desc", 1);

            var act = () => product.IncreaseQuantity(0);

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        [Fact]
        public void UpdateDetails_ValidParameters_ShouldUpdateAllFields()
        {
            var (product, _) = Product.Create("Test", 10m, 5, "Desc", 1);

            product.UpdateDetails("Updated", 20m, "New desc", 2);

            product.Name.Value.Should().Be("Updated");
            product.Cost.Amount.Should().Be(20m);
            product.Description.Value.Should().Be("New desc");
            product.CategoryId.Value.Should().Be(2);
        }

        [Fact]
        public void AddTag_NewTag_ShouldAddToCollection()
        {
            var (product, _) = Product.Create("Test", 10m, 5, "Desc", 1);

            product.AddTag(new TagId(1));

            product.ProductTags.Should().HaveCount(1);
            product.ProductTags[0].TagId.Value.Should().Be(1);
        }

        [Fact]
        public void AddTag_DuplicateTag_ShouldNotAddTwice()
        {
            var (product, _) = Product.Create("Test", 10m, 5, "Desc", 1);

            product.AddTag(new TagId(1));
            product.AddTag(new TagId(1));

            product.ProductTags.Should().HaveCount(1);
        }

        [Fact]
        public void RemoveTag_ExistingTag_ShouldRemoveFromCollection()
        {
            var (product, _) = Product.Create("Test", 10m, 5, "Desc", 1);
            product.AddTag(new TagId(1));
            product.AddTag(new TagId(2));

            product.RemoveTag(new TagId(1));

            product.ProductTags.Should().HaveCount(1);
            product.ProductTags[0].TagId.Value.Should().Be(2);
        }

        [Fact]
        public void AddImage_FirstImage_ShouldBeSetAsMain()
        {
            var (product, _) = Product.Create("Test", 10m, 5, "Desc", 1);

            product.AddImage("items/1/test.jpg");

            product.Images.Should().HaveCount(1);
            product.Images[0].IsMain.Should().BeTrue();
            product.Images[0].SortOrder.Should().Be(0);
        }

        [Fact]
        public void AddImage_SecondImage_ShouldNotBeMainAndSortOrderIncremented()
        {
            var (product, _) = Product.Create("Test", 10m, 5, "Desc", 1);
            product.AddImage("items/1/first.jpg");

            product.AddImage("items/1/second.jpg");

            product.Images[1].IsMain.Should().BeFalse();
            product.Images[1].SortOrder.Should().Be(1);
        }

        [Fact]
        public void AddImage_MoreThanFive_ShouldThrowDomainException()
        {
            var (product, _) = Product.Create("Test", 10m, 5, "Desc", 1);
            for (int i = 0; i < 5; i++)
                product.AddImage($"items/1/img{i}.jpg");

            var act = () => product.AddImage("items/1/extra.jpg");

            act.Should().Throw<DomainException>().WithMessage("*at most 5*");
        }

        [Fact]
        public void SetMainImage_NotExistingImage_ShouldThrowDomainException()
        {
            var (product, _) = Product.Create("Test", 10m, 5, "Desc", 1);

            var act = () => product.SetMainImage(999);

            act.Should().Throw<DomainException>().WithMessage("*not found*");
        }

        [Fact]
        public void RemoveImage_MainImage_ShouldPromoteFirstRemainingAsMain()
        {
            var (product, _) = Product.Create("Test", 10m, 5, "Desc", 1);
            product.AddImage("items/1/first.jpg");
            product.AddImage("items/1/second.jpg");
            var firstId = product.Images[0].Id.Value;

            product.RemoveImage(firstId);

            product.Images.Should().HaveCount(1);
            product.Images[0].IsMain.Should().BeTrue();
            product.Images[0].SortOrder.Should().Be(0);
        }

        [Fact]
        public void ReorderImages_WrongCount_ShouldThrowDomainException()
        {
            var (product, _) = Product.Create("Test", 10m, 5, "Desc", 1);
            product.AddImage("items/1/first.jpg");
            product.AddImage("items/1/second.jpg");

            var act = () => product.ReorderImages(new[] { 0 });

            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void ReorderImages_MissingImageId_ShouldThrowDomainException()
        {
            var (product, _) = Product.Create("Test", 10m, 5, "Desc", 1);
            product.AddImage("items/1/first.jpg");

            var act = () => product.ReorderImages(new[] { 999 });

            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void ReplaceTags_NewTagSet_ShouldReplaceAll()
        {
            var (product, _) = Product.Create("Test", 10m, 5, "Desc", 1);
            product.AddTag(new TagId(1));
            product.AddTag(new TagId(2));

            product.ReplaceTags(new[] { new TagId(3), new TagId(4), new TagId(5) });

            product.ProductTags.Should().HaveCount(3);
        }

        [Fact]
        public void Publish_UnpublishedProduct_ShouldRepublish()
        {
            var (product, _) = Product.Create("Test", 10m, 1, "Desc", 1);
            product.Publish();
            product.Unpublish();

            var @event = product.Publish();

            product.Status.Should().Be(ProductStatus.Published);
            @event.Should().NotBeNull();
        }
    }
}
