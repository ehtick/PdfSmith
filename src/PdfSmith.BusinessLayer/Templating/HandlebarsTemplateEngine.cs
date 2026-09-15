using System.Collections.Concurrent;
using System.Globalization;
using HandlebarsDotNet;
using HandlebarsDotNet.Helpers;
using HandlebarsDotNet.Helpers.Enums;
using HandlebarsDotNet.Helpers.Utils;
using PdfSmith.BusinessLayer.Exceptions;
using PdfSmith.BusinessLayer.Services;
using PdfSmith.BusinessLayer.Templating.Interfaces;

namespace PdfSmith.BusinessLayer.Templating;

public class HandlebarsTemplateEngine(ClientTimeProvider clientTimeProvider) : ITemplateEngine
{
    private readonly ConcurrentDictionary<string, IHandlebars> handlebarsInstances = new(StringComparer.OrdinalIgnoreCase);

    public Task<string> RenderAsync(string template, object? model, CultureInfo culture, CancellationToken cancellationToken = default)
    {
        try
        {
            var handlebars = handlebarsInstances.GetOrAdd(culture.Name, _ => CreateHandlebarsInstance(clientTimeProvider, culture));
            var compiledTemplate = handlebars.Compile(template);

            cancellationToken.ThrowIfCancellationRequested();

            var result = compiledTemplate(new { Model = model });
            return Task.FromResult(result);
        }
        catch (HandlebarsException ex)
        {
            throw new TemplateEngineException(ex.Message, ex);
        }
        catch (Exception ex)
        {
            throw new TemplateEngineException($"An error occurred while rendering the Handlebars template: {ex.Message}", ex);
        }
    }

    private static IHandlebars CreateHandlebarsInstance(ClientTimeProvider clientTimeProvider, CultureInfo culture)
    {
        var handlebars = Handlebars.Create();
        handlebars.Configuration.FormatProvider = culture;

        HandlebarsHelpers.Register(handlebars, options =>
        {
            options.Categories = [Category.Boolean, Category.DateTime, Category.Math, Category.String];
            options.UseCategoryPrefix = false;
            options.DateTimeService = new HandlerbasDateTimeService(clientTimeProvider);
        });

        return handlebars;
    }

    private class HandlerbasDateTimeService(ClientTimeProvider clientTimeProvider) : IDateTimeService
    {
        public DateTime Now() => clientTimeProvider.GetLocalNow().DateTime;

        public DateTime UtcNow() => clientTimeProvider.GetUtcNow().DateTime;
    }
}