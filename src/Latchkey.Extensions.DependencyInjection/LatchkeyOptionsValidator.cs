using Latchkey;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Validates <see cref="LatchkeyOptions.ServiceName"/> using the same rules as the core library.</summary>
internal sealed class LatchkeyOptionsValidator : IValidateOptions<LatchkeyOptions>
{
    public ValidateOptionsResult Validate(string? name, LatchkeyOptions options)
    {
        try
        {
            Validation.ValidateServiceName(options.ServiceName);
        }
        catch (ArgumentException ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }

        return ValidateOptionsResult.Success;
    }
}
