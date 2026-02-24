using ECommerceApp.Domain.Catalog.Products;
using ECommerceApp.Domain.Catalog.Products.ValueObjects;
using ECommerceApp.Domain.Shared;
using FluentAssertions;
using Xunit;

namespace ECommerceApp.UnitTests.Catalog.Products
{
    public class ValueObjectTests
    {
        [Fact]
        public void ProductName_ValidName_ShouldCreate()
        {
            var name = new ProductName("Test Product");

            name.Value.Should().Be("Test Product");
        }

        [Fact]
        public void ProductName_EmptyString_ShouldThrowDomainException()
        {
            var act = () => new ProductName("");

            act.Should().Throw<DomainException>().WithMessage("*required*");
        }

        [Fact]
        public void ProductName_TooShort_ShouldThrowDomainException()
        {
            var act = () => new ProductName("ab");

            act.Should().Throw<DomainException>().WithMessage("*at least 3*");
        }

        [Fact]
        public void ProductName_TooLong_ShouldThrowDomainException()
        {
            var act = () => new ProductName(new string('a', 151));

            act.Should().Throw<DomainException>().WithMessage("*exceed 150*");
        }

        [Fact]
        public void ProductName_ShouldTrimWhitespace()
        {
            var name = new ProductName("  Trimmed  ");

            name.Value.Should().Be("Trimmed");
        }

        [Fact]
        public void Slug_ValidSlug_ShouldCreate()
        {
            var slug = new Slug("test-product");

            slug.Value.Should().Be("test-product");
        }

        [Fact]
        public void Slug_EmptyString_ShouldThrowDomainException()
        {
            var act = () => new Slug("");

            act.Should().Throw<DomainException>().WithMessage("*required*");
        }

        [Fact]
        public void Slug_InvalidChars_ShouldThrowDomainException()
        {
            var act = () => new Slug("UPPER CASE!");

            act.Should().Throw<DomainException>().WithMessage("*lowercase*");
        }

        [Fact]
        public void Slug_FromName_ShouldNormalize()
        {
            var slug = Slug.FromName("Test Product Name");

            slug.Value.Should().Be("test-product-name");
        }

        [Fact]
        public void Slug_FromName_ShouldRemoveSpecialChars()
        {
            var slug = Slug.FromName("Product (Special!) @Edition");

            slug.Value.Should().Be("product-special-edition");
        }

        [Fact]
        public void Price_ValidAmount_ShouldCreate()
        {
            var price = new Price(10.50m);

            price.Amount.Should().Be(10.50m);
        }

        [Fact]
        public void Price_ZeroAmount_ShouldThrowDomainException()
        {
            var act = () => new Price(0);

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        [Fact]
        public void Price_NegativeAmount_ShouldThrowDomainException()
        {
            var act = () => new Price(-5m);

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        [Fact]
        public void Price_ToMoney_ShouldCreatePlnMoney()
        {
            var price = new Price(100m);

            var money = price.ToMoney();

            money.Amount.Should().Be(100m);
            money.CurrencyCode.Should().Be("PLN");
            money.Rate.Should().Be(1m);
        }

        [Fact]
        public void Money_ValidParameters_ShouldCreate()
        {
            var money = new Money(100m, "EUR", 4.25m);

            money.Amount.Should().Be(100m);
            money.CurrencyCode.Should().Be("EUR");
            money.Rate.Should().Be(4.25m);
        }

        [Fact]
        public void Money_Pln_ShouldCreatePlnWithRate1()
        {
            var money = Money.Pln(50m);

            money.Amount.Should().Be(50m);
            money.CurrencyCode.Should().Be("PLN");
            money.Rate.Should().Be(1m);
        }

        [Fact]
        public void Money_ToBaseCurrency_ShouldMultiplyAmountByRate()
        {
            var money = new Money(100m, "EUR", 4.25m);

            money.ToBaseCurrency().Should().Be(425m);
        }

        [Fact]
        public void Money_ZeroAmount_ShouldThrowDomainException()
        {
            var act = () => new Money(0, "PLN", 1m);

            act.Should().Throw<DomainException>().WithMessage("*positive*");
        }

        [Fact]
        public void Money_EmptyCurrencyCode_ShouldThrowDomainException()
        {
            var act = () => new Money(100m, "", 1m);

            act.Should().Throw<DomainException>().WithMessage("*Currency code*");
        }

        [Fact]
        public void Money_ZeroRate_ShouldThrowDomainException()
        {
            var act = () => new Money(100m, "PLN", 0);

            act.Should().Throw<DomainException>().WithMessage("*Rate*positive*");
        }

        [Fact]
        public void Money_ShouldUppercaseCurrencyCode()
        {
            var money = new Money(100m, "eur", 4.25m);

            money.CurrencyCode.Should().Be("EUR");
        }

        [Fact]
        public void Category_Create_ValidParameters_ShouldCreate()
        {
            var category = Category.Create("Electronics");

            category.Name.Value.Should().Be("Electronics");
            category.Slug.Value.Should().Be("electronics");
        }

        [Fact]
        public void Category_Create_EmptyName_ShouldThrowDomainException()
        {
            var act = () => Category.Create("");

            act.Should().Throw<DomainException>().WithMessage("*required*");
        }

        [Fact]
        public void Category_Create_NameTooLong_ShouldThrowDomainException()
        {
            var act = () => Category.Create(new string('a', 101));

            act.Should().Throw<DomainException>().WithMessage("*100*");
        }

        [Fact]
        public void CategorySlug_ValidValue_ShouldCreate()
        {
            var slug = new CategorySlug("electronics");

            slug.Value.Should().Be("electronics");
        }

        [Fact]
        public void CategorySlug_TooLong_ShouldThrowDomainException()
        {
            var act = () => new CategorySlug(new string('a', 50) + "-" + new string('b', 50));

            act.Should().Throw<DomainException>().WithMessage("*100*");
        }

        [Fact]
        public void CategorySlug_FromName_ShouldGenerateCorrectSlug()
        {
            var slug = CategorySlug.FromName("Home Appliances");

            slug.Value.Should().Be("home-appliances");
        }

        [Fact]
        public void TagSlug_ValidValue_ShouldCreate()
        {
            var slug = new TagSlug("sale");

            slug.Value.Should().Be("sale");
        }

        [Fact]
        public void TagSlug_TooLong_ShouldThrowDomainException()
        {
            var act = () => new TagSlug("abcdefghij-abcdefghij-abcdefghi");

            act.Should().Throw<DomainException>().WithMessage("*30*");
        }

        [Fact]
        public void TagSlug_FromName_ShouldGenerateCorrectSlug()
        {
            var slug = TagSlug.FromName("Nowy");

            slug.Value.Should().Be("nowy");
        }

        [Fact]
        public void Tag_Create_ValidParameters_ShouldCreate()
        {
            var tag = Tag.Create("czerwony");

            tag.Name.Value.Should().Be("czerwony");
            tag.Slug.Value.Should().Be("czerwony");
        }

        [Fact]
        public void Tag_Create_EmptyName_ShouldThrowDomainException()
        {
            var act = () => Tag.Create("");

            act.Should().Throw<DomainException>().WithMessage("*required*");
        }

        [Fact]
        public void Tag_Create_NameTooLong_ShouldThrowDomainException()
        {
            var act = () => Tag.Create(new string('a', 51));

            act.Should().Throw<DomainException>().WithMessage("*50*");
        }

        [Fact]
        public void ImageFileName_ValidValue_ShouldCreate()
        {
            var fn = new ImageFileName("items/1/photo.jpg");

            fn.Value.Should().Be("items/1/photo.jpg");
        }

        [Fact]
        public void ImageFileName_EmptyString_ShouldThrowDomainException()
        {
            var act = () => new ImageFileName("");

            act.Should().Throw<DomainException>().WithMessage("*required*");
        }

        [Fact]
        public void ImageFileName_TooLong_ShouldThrowDomainException()
        {
            var act = () => new ImageFileName(new string('a', 501));

            act.Should().Throw<DomainException>().WithMessage("*500*");
        }

        [Fact]
        public void ProductDescription_ValidValue_ShouldCreate()
        {
            var desc = new ProductDescription("Great product");

            desc.Value.Should().Be("Great product");
        }

        [Fact]
        public void ProductDescription_NullValue_ShouldDefaultToEmpty()
        {
            var desc = new ProductDescription(null);

            desc.Value.Should().Be("");
        }

        [Fact]
        public void ProductDescription_TooLong_ShouldThrowDomainException()
        {
            var act = () => new ProductDescription(new string('a', 301));

            act.Should().Throw<DomainException>().WithMessage("*300*");
        }

        [Fact]
        public void ProductDescription_ShouldTrimWhitespace()
        {
            var desc = new ProductDescription("  trimmed  ");

            desc.Value.Should().Be("trimmed");
        }

        [Fact]
        public void ProductQuantity_ValidValue_ShouldCreate()
        {
            var qty = new ProductQuantity(10);

            qty.Value.Should().Be(10);
        }

        [Fact]
        public void ProductQuantity_ZeroValue_ShouldCreate()
        {
            var qty = new ProductQuantity(0);

            qty.Value.Should().Be(0);
        }

        [Fact]
        public void ProductQuantity_NegativeValue_ShouldThrowDomainException()
        {
            var act = () => new ProductQuantity(-1);

            act.Should().Throw<DomainException>().WithMessage("*negative*");
        }

        [Fact]
        public void ProductQuantity_ImplicitConversion_ShouldReturnInt()
        {
            var qty = new ProductQuantity(5);
            int value = qty;

            value.Should().Be(5);
        }

        [Fact]
        public void TagName_ValidName_ShouldCreate()
        {
            var name = new TagName("Sale");

            name.Value.Should().Be("Sale");
        }

        [Fact]
        public void TagName_TooLong_ShouldThrowDomainException()
        {
            var act = () => new TagName(new string('a', 51));

            act.Should().Throw<DomainException>().WithMessage("*50*");
        }

        [Fact]
        public void TagName_EmptyString_ShouldThrowDomainException()
        {
            var act = () => new TagName("");

            act.Should().Throw<DomainException>().WithMessage("*required*");
        }

        [Fact]
        public void CategoryName_ValidName_ShouldCreate()
        {
            var name = new CategoryName("Computers");

            name.Value.Should().Be("Computers");
        }

        [Fact]
        public void CategoryName_TooLong_ShouldThrowDomainException()
        {
            var act = () => new CategoryName(new string('a', 101));

            act.Should().Throw<DomainException>().WithMessage("*100*");
        }

        [Fact]
        public void CategoryName_EmptyString_ShouldThrowDomainException()
        {
            var act = () => new CategoryName("");

            act.Should().Throw<DomainException>().WithMessage("*required*");
        }
    }
}
