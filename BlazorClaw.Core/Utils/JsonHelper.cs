using BlazorClaw.Core.Providers;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorClaw.Core.Utils
{
    public class JsonHelper
    {
        public static JsonSerializerOptions DefaultOptions
        {
            get
            {
                var jo = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true,
                    IgnoreReadOnlyFields = true,
                    IgnoreReadOnlyProperties = true,
                    WriteIndented = true
                };
                jo.Converters.Add(new JsonStringEnumConverter());
                return jo;
            }
        }
    }

    public static class DependencyInjectionExtensions
    {
        private readonly static Type? constantCallSiteType;
        private readonly static Type? serviceIdentifierType;
        private readonly static ConstructorInfo? constantCallSiteConstructor;
        private readonly static MethodInfo? fromServiceTypeMethod;

        static DependencyInjectionExtensions()
        {
            constantCallSiteType = typeof(ServiceProvider).Assembly.GetType("Microsoft.Extensions.DependencyInjection.ServiceLookup.ConstantCallSite");
            serviceIdentifierType = typeof(ServiceProvider).Assembly.GetType("Microsoft.Extensions.DependencyInjection.ServiceLookup.ServiceIdentifier");

            constantCallSiteConstructor = constantCallSiteType?.GetConstructor([typeof(Type), typeof(object)]);
            fromServiceTypeMethod = serviceIdentifierType?.GetMethod("FromServiceType");
        }

        public static void AddScopedPostBuild<TService>(this ServiceProvider serviceProvider, Func<IServiceProvider, TService> implementationFactory) where TService : class
        {
            var serviceType = typeof(TService);
            var callSiteFactory = serviceProvider.GetPrivatePropertyValue<object>("CallSiteFactory");
            var serviceIdentifier = fromServiceTypeMethod?.Invoke(null, [serviceType]);
            var objImplementation = implementationFactory(serviceProvider);
            var callSite = constantCallSiteConstructor?.Invoke([serviceType, objImplementation]);
            if (serviceIdentifier != null && callSite != null)
                callSiteFactory?.CallMethod("Add", serviceIdentifier, callSite);
        }
        public static T? GetPrivatePropertyValue<T>(this object obj, string property)
        {
            var type = obj.GetType();
            var propertyInfo = type.GetProperty(property, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.GetProperty);
            T? value = default;

            if (propertyInfo != null)
            {
                value = (T?)propertyInfo.GetValue(obj, null);
            }
            else
            {
                var baseType = type.BaseType;

                while (baseType != null)
                {
                    propertyInfo = type.GetProperty(property, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.GetProperty);

                    if (propertyInfo != null)
                    {
                        value = (T?)propertyInfo.GetValue(obj, null);
                        break;
                    }

                    baseType = baseType.BaseType;
                }
            }

            return value;
        }
        public static T? CallMethod<T>(this object obj, string methodName, params object[] args)
        {
            return (T?)obj.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)?.Invoke(obj, args);
        }

        public static object? CallMethod(this object obj, string methodName, params object[] args)
        {
            return obj.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)?.Invoke(obj, args);
        }
    }

    public static class UtilsExtensions
    {
        public static void InitProvider(this HttpClient httpClient, IProviderConfiguration conf)
        {
            httpClient.BaseAddress = new Uri(conf.Uri.TrimEnd('/') + "/");
            if (!string.IsNullOrWhiteSpace(conf.Token))
            {
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", conf.Token);
                httpClient.DefaultRequestHeaders.Add("X-Api-Key", conf.Token);
            }
        }
    }
}
