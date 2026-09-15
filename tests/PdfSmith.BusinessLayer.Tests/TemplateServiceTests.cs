using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PdfSmith.BusinessLayer.Services;
using PdfSmith.BusinessLayer.Services.Interfaces;
using PdfSmith.BusinessLayer.Templating;
using PdfSmith.BusinessLayer.Templating.Interfaces;
using PdfSmith.Shared.Models;

namespace PdfSmith.BusinessLayer.Tests;

public class TemplateServiceTests
{
    private readonly IMarkdownConverter markdownConverter = Substitute.For<IMarkdownConverter>();
    private readonly ITimeZoneService timeZoneService = Substitute.For<ITimeZoneService>();
    private readonly ITemplateEngine templateEngine = Substitute.For<ITemplateEngine>();

    private TemplateService CreateService()
    {
        var serviceProvider = Substitute.For<IServiceProvider, IKeyedServiceProvider>();
        ((IKeyedServiceProvider)serviceProvider).GetKeyedService<ITemplateEngine>("scriban").Returns(templateEngine);

        return new(serviceProvider, timeZoneService, markdownConverter);
    }

    [Fact]
    public async Task CreateAsync_WhenContentIsMarkdown_ConvertsToHtml()
    {
        var service = CreateService();
        var markdownContent = "# Hello World";
        var expectedHtml = "<h1>Hello World</h1>\n";

        templateEngine.RenderAsync(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<CultureInfo>(), Arg.Any<CancellationToken>()).Returns(markdownContent);
        markdownConverter.IsMarkdownAsync(markdownContent).Returns(true);
        markdownConverter.ConvertToHtmlAsync(markdownContent).Returns(expectedHtml);
        timeZoneService.GetTimeZone().Returns(TimeZoneInfo.Utc);

        var request = new TemplateGenerationRequest(markdownContent, null, "scriban");

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(expectedHtml, result.Content!.Result);
        await markdownConverter.Received(1).ConvertToHtmlAsync(markdownContent);
    }

    [Fact]
    public async Task CreateAsync_WhenContentIsHtml_DoesNotConvert()
    {
        var service = CreateService();
        var htmlContent = "<html><body><h1>Hello</h1></body></html>";

        templateEngine.RenderAsync(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<CultureInfo>(), Arg.Any<CancellationToken>()).Returns(htmlContent);
        markdownConverter.IsMarkdownAsync(htmlContent).Returns(false);
        timeZoneService.GetTimeZone().Returns(TimeZoneInfo.Utc);

        var request = new TemplateGenerationRequest(htmlContent, null, "scriban");

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(htmlContent, result.Content!.Result);
        await markdownConverter.DidNotReceive().ConvertToHtmlAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task HandlebarsRenderAsync_WithDifferentCultures_UsesRequestedCulture()
    {
        timeZoneService.GetTimeZone().Returns(TimeZoneInfo.Utc);
        var engine = new HandlebarsTemplateEngine(new ClientTimeProvider(timeZoneService));
        const string template = "{{Format Model.Amount \"C\"}}";
        var model = new { Amount = 5.2 };
        var italianCulture = CultureInfo.GetCultureInfo("it-IT");
        var americanCulture = CultureInfo.GetCultureInfo("en-US");
        var cancellationToken = TestContext.Current.CancellationToken;

        var italianResult = await engine.RenderAsync(template, model, italianCulture, cancellationToken);
        var americanResult = await engine.RenderAsync(template, model, americanCulture, cancellationToken);

        Assert.Equal(5.2.ToString("C", italianCulture), italianResult);
        Assert.Equal(5.2.ToString("C", americanCulture), americanResult);
    }
}
