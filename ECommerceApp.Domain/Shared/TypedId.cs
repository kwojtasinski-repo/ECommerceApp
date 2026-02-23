namespace ECommerceApp.Domain.Shared
{
    public abstract record TypedId<T>(T Value)
    {
        public static implicit operator T(TypedId<T> typedId) => typedId.Value;

        public override string ToString() => Value?.ToString() ?? string.Empty;
    }
}
