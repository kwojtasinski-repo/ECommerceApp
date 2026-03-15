using System.Collections.Generic;

namespace ECommerceApp.Application.Exceptions
{
    public sealed record ErrorCodeDto(string Code,
        IReadOnlyList<ErrorParameterDto> Parameters);

    public sealed record ErrorParameterDto(string Name, string Value);
}
