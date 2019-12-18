using System.Collections.Generic;
using System.Linq;
using Castle.MicroKernel;
using Castle.MicroKernel.Registration;
using Castle.Windsor;

namespace System.Net.Http.Windsor
{
    public static class ContainerExtensions
    {
        
        public static IWindsorContainer AddHttpClient<TTypedClient, TTypedClientImplementation>(
            this IWindsorContainer container,
            Action<HttpClient> configureHttpClient,
            IEnumerable<Type> delegatingHandlerTypes
        )
            where TTypedClient : class
            where TTypedClientImplementation : class, TTypedClient
        {
            var httpClientComponentName = $"HttpClient_For_{typeof(TTypedClient).FullName}";

            //
            // Default HttpClient (first registered is default in Windsor)
            //
            container.Register(
                Component.For<HttpClient>()
                .LifeStyle.Singleton);

            //
            // HttpClient specifically for this typed client
            //
            container.Register(
                Component.For<HttpClient>()
                    .Named(httpClientComponentName)
                    .UsingFactoryMethod(kernel =>
                    {
                        var pipeline = BuildHandlerPipeline(kernel, delegatingHandlerTypes);
                        var httpClient = new HttpClient(pipeline, true);
                        configureHttpClient(httpClient);
                        return httpClient;
                    })
                    .LifeStyle.Singleton);

            //
            // The typed client
            //
            container.Register(
                Component.For<TTypedClient>()
                    .ImplementedBy<TTypedClientImplementation>()
                    .DependsOn(Dependency.OnComponent(typeof(HttpClient), httpClientComponentName))
                    .LifeStyle.Transient);

            return container;
        }


        private static HttpMessageHandler BuildHandlerPipeline(IKernel kernel, IEnumerable<Type> handlerTypes)
        {
            var handlers =
                handlerTypes
                    .Select(type => (DelegatingHandler)kernel.Resolve(type))
                    .ToList();

            for (int i = 0; i < handlers.Count - 1; i++)
            {
                handlers[i].InnerHandler = handlers[i+1];
            }

            var httpClientHandler = new HttpClientHandler();

            if (handlers.Any())
            {
                handlers.Last().InnerHandler = httpClientHandler;
            }

            var firstHandler = (HttpMessageHandler)handlers.FirstOrDefault() ?? httpClientHandler;

            return firstHandler;
        }

    }
}
