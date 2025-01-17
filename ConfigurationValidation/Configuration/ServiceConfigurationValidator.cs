﻿using Microsoft.Extensions.Options;

namespace WebApplication1;

public class ServiceConfigurationValidator : IValidateOptions<ServiceConfiguration>
{
    public ValidateOptionsResult Validate(string? name, ServiceConfiguration options)
    {
        var validationResult = "";

        if (string.IsNullOrEmpty(options.ApiKey))
        {
            validationResult += "Api key is missing";
        }
        if (options.LowestPriority > options.HighestPriority)
        {
            validationResult += "Lowest priority must be lower than higest priority";
        }
        if (!string.IsNullOrEmpty(validationResult))
        {
            return ValidateOptionsResult.Fail(validationResult);
        }

        return ValidateOptionsResult.Success;
    }
}