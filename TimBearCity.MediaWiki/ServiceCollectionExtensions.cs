using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using TimBearCity.MediaWiki;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registration helpers for <see cref="IMediaWikiClient"/>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>The options name and service key used by the unnamed, single-wiki registration.</summary>
    private static readonly string DefaultName = Options.Options.DefaultName;

    /// <summary>
    /// Registers <see cref="IMediaWikiClient"/> as a typed <see cref="HttpClient"/>, configured in code.
    /// </summary>
    /// <param name="services">The service collection to register the client in.</param>
    /// <param name="configureOptions">Configures the wiki this client talks to.</param>
    /// <returns>The builder for the underlying <see cref="HttpClient"/>, so handlers can be added to it.</returns>
    /// <exception cref="InvalidOperationException">A client is already registered for the default wiki.</exception>
    public static IHttpClientBuilder AddMediaWikiClient(this IServiceCollection services, Action<MediaWikiOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        return services.AddMediaWikiClient(DefaultName, builder => builder.Configure(configureOptions));
    }

    /// <summary>
    /// Registers <see cref="IMediaWikiClient"/> as a typed <see cref="HttpClient"/>, bound to a
    /// configuration section (<see cref="MediaWikiOptions.Position"/> by default).
    /// </summary>
    /// <param name="services">The service collection to register the client in.</param>
    /// <param name="configurationSection">The section holding the <see cref="MediaWikiOptions"/> values.</param>
    /// <returns>The builder for the underlying <see cref="HttpClient"/>, so handlers can be added to it.</returns>
    /// <exception cref="InvalidOperationException">A client is already registered for the default wiki.</exception>
    public static IHttpClientBuilder AddMediaWikiClient(this IServiceCollection services, IConfiguration configurationSection)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configurationSection);

        return services.AddMediaWikiClient(DefaultName, ConfigureFrom(configurationSection));
    }

    /// <summary>
    /// Registers a further wiki under <paramref name="name"/>, configured in code. Each wiki gets its own options,
    /// <see cref="HttpClient"/> and handler pipeline, and is resolved as a keyed service:
    /// <c>[FromKeyedServices("commons")] IMediaWikiClient client</c>.
    /// </summary>
    /// <param name="services">The service collection to register the client in.</param>
    /// <param name="name">The key the client is resolved by. Must be unique and non-empty.</param>
    /// <param name="configureOptions">Configures the wiki this client talks to.</param>
    /// <returns>The builder for the underlying <see cref="HttpClient"/>, so handlers can be added to it.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">A client is already registered under <paramref name="name"/>.</exception>
    public static IHttpClientBuilder AddMediaWikiClient(this IServiceCollection services, string name, Action<MediaWikiOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configureOptions);

        return services.AddMediaWikiClient(name, builder => builder.Configure(configureOptions));
    }

    /// <summary>
    /// Registers a further wiki under <paramref name="name"/>, bound to a configuration section. Each wiki gets its
    /// own options, <see cref="HttpClient"/> and handler pipeline, and is resolved as a keyed service:
    /// <c>[FromKeyedServices("commons")] IMediaWikiClient client</c>.
    /// </summary>
    /// <param name="services">The service collection to register the client in.</param>
    /// <param name="name">The key the client is resolved by. Must be unique and non-empty.</param>
    /// <param name="configurationSection">The section holding this wiki's <see cref="MediaWikiOptions"/> values.</param>
    /// <returns>The builder for the underlying <see cref="HttpClient"/>, so handlers can be added to it.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">A client is already registered under <paramref name="name"/>.</exception>
    public static IHttpClientBuilder AddMediaWikiClient(this IServiceCollection services, string name, IConfiguration configurationSection)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configurationSection);

        return services.AddMediaWikiClient(name, ConfigureFrom(configurationSection));
    }

    private static IHttpClientBuilder AddMediaWikiClient(this IServiceCollection services, string name, Action<OptionsBuilder<MediaWikiOptions>> configure)
    {
        if (services.Any(descriptor => IsMediaWikiClient(descriptor, name)))
        {
            throw new InvalidOperationException(name == DefaultName
                ? $"A MediaWiki client is already registered. Call {nameof(AddMediaWikiClient)}(name, ...) to register further wikis."
                : $"A MediaWiki client is already registered for wiki \"{name}\". Register each wiki once, under its own name.");
        }

        var optionsBuilder = services.AddOptions<MediaWikiOptions>(name);
        configure(optionsBuilder);

        optionsBuilder.ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MediaWikiOptions>>(new MediaWikiOptionsValidator()));

        IHttpClientBuilder builder;

        if (name == DefaultName)
        {
            builder = services.AddHttpClient<IMediaWikiClient, MediaWikiClient>(ConfigureHttpClient(name));
        }
        else
        {
            var httpClientName = BuildHttpClientName(name);
            builder = services.AddHttpClient(httpClientName, ConfigureHttpClient(name));

            // Transient, matching the lifetime a typed client would have: the handler pipeline is pooled by
            // IHttpClientFactory, and a longer-lived HttpClient would pin one past its rotation.
            services.AddKeyedTransient<IMediaWikiClient>(name, (serviceProvider, _) =>
                new MediaWikiClient(serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(httpClientName)));
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter>(new RedirectHandlerBuilderFilter()));

        // Added here rather than with AddHttpMessageHandler, since which handlers a wiki gets depends on its options.
        return builder.ConfigureAdditionalHttpMessageHandlers((handlers, serviceProvider) =>
            MediaWikiPipeline.AddHandlers(handlers, serviceProvider.GetRequiredService<IOptionsMonitor<MediaWikiOptions>>().Get(name)));
    }

    /// <summary>Copies the values present in the section onto the options, leaving absent keys at their default.</summary>
    /// <exception cref="InvalidOperationException">A value is present but cannot be converted.</exception>
    private static void Bind(MediaWikiOptions options, IConfiguration configurationSection)
    {
        if (configurationSection[nameof(MediaWikiOptions.AccessToken)] is { } accessToken)
        {
            options.AccessToken = accessToken;
        }

        if (configurationSection[nameof(MediaWikiOptions.BaseUrl)] is { } baseUrl)
        {
            options.BaseUrl = baseUrl;
        }

        if (configurationSection[nameof(MediaWikiOptions.UserAgent)] is { } userAgent)
        {
            options.UserAgent = userAgent;
        }

        if (configurationSection[nameof(MediaWikiOptions.Timeout)] is { } timeout)
        {
            options.Timeout = TimeSpan.TryParse(timeout, CultureInfo.InvariantCulture, out var parsedTimeout)
                ? parsedTimeout
                : throw new InvalidOperationException(
                    $"Failed to convert configuration value at '{configurationSection.GetSection(nameof(MediaWikiOptions.Timeout)).Path}' to type '{nameof(TimeSpan)}'.");
        }

        if (configurationSection[nameof(MediaWikiOptions.MaxResponseSize)] is { } maxResponseSize)
        {
            options.MaxResponseSize = int.TryParse(maxResponseSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMaxResponseSize)
                ? parsedMaxResponseSize
                : throw new InvalidOperationException(
                    $"Failed to convert configuration value at '{configurationSection.GetSection(nameof(MediaWikiOptions.MaxResponseSize)).Path}' to type '{nameof(Int32)}'.");
        }
    }

    /// <summary>Names the underlying <see cref="HttpClient"/>; the ':' cannot collide with the default typed client's type name.</summary>
    private static string BuildHttpClientName(string name)
    {
        return $"{MediaWikiOptions.Position}:{name}";
    }

    /// <summary>
    /// Binds the options to a configuration section, and keeps <see cref="IOptionsMonitor{TOptions}"/> reloading as
    /// that section changes, the way <c>OptionsBuilder.Bind</c> would.
    /// </summary>
    /// <remarks>
    /// The binding is written out by hand rather than delegated to the configuration binder, whose reflection over
    /// <see cref="MediaWikiOptions"/> is not safe under trimming or Native AOT.
    /// </remarks>
    private static Action<OptionsBuilder<MediaWikiOptions>> ConfigureFrom(IConfiguration configurationSection)
    {
        return optionsBuilder =>
        {
            optionsBuilder.Services.AddSingleton<IOptionsChangeTokenSource<MediaWikiOptions>>(
                new ConfigurationChangeTokenSource<MediaWikiOptions>(optionsBuilder.Name, configurationSection));

            optionsBuilder.Configure(options => Bind(options, configurationSection));
        };
    }

    private static Action<IServiceProvider, HttpClient> ConfigureHttpClient(string name)
    {
        return (serviceProvider, client) =>
            MediaWikiPipeline.ConfigureHttpClient(client, serviceProvider.GetRequiredService<IOptionsMonitor<MediaWikiOptions>>().Get(name));
    }

    private static bool IsMediaWikiClient(ServiceDescriptor descriptor, string name)
    {
        if (descriptor.ServiceType != typeof(IMediaWikiClient))
        {
            return false;
        }

        return name == DefaultName
            ? !descriptor.IsKeyedService
            : descriptor.IsKeyedService && descriptor.ServiceKey as string == name;
    }
}
