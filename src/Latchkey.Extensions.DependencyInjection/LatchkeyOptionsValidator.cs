using Microsoft.Extensions.Options;

namespace Latchkey.Extensions.DependencyInjection;

/// <summary>Validates <see cref="LatchkeyOptions.ServiceName" /> using the same rules as the core library.</summary>
sealed class LatchkeyOptionsValidator : IValidateOptions<LatchkeyOptions>
{
    public ValidateOptionsResult Validate(string? name, LatchkeyOptions options)
    {
        try
        {
            Validation.ValidateServiceName(options.ServiceName);
            Validation.ValidateBackendOptions(options.BackendOptions);
            Validation.ValidateBackendMap(options.Backends);
        }
        catch (ArgumentException ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }

        return ValidateOptionsResult.Success;
    }
}
