using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Nucleus.Web {
    public class RouteBuilder {
        public static List<Route> BuildRoute(Assembly baseAssembly) {
            List<Route> routes = new List<Route>();
            Type[] allTypes = baseAssembly.GetTypes();

            List<object> managers = new List<object>();

            for (int i = 0; i < allTypes.Length; i++) {
                Type type = allTypes[i];
                object[] attributes = type.GetCustomAttributes(true);

                for (int j = 0; j < attributes.Length; j++) {
                    object attribute = attributes[j];

                    if (attribute is RouteManagerAttribute) {
                        RouteManagerAttribute manager = (RouteManagerAttribute)attribute;
                        object routeManager = Activator.CreateInstance(type);
                        managers.Add(routeManager);

                        string baseUrl = manager.UrlRegex;
                        ConsoleU.WriteLine($"Route Manager: {baseUrl}", ConsoleColor.Green);

                        MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                        for (int k = 0; k < methods.Length; k++) {
                            MethodInfo method = methods[k];
                            object[] mAttributes = method.GetCustomAttributes(true);

                            for (int u = 0; u < mAttributes.Length; u++) {
                                object mAttribute = mAttributes[u];

                                if (mAttribute is RouteAttribute) {
                                    RouteAttribute routeAtt = (RouteAttribute)mAttribute;
                                    Route route = routeAtt.Route;
                                    string urlRegex = route.UrlRegex;
                                    if (string.IsNullOrEmpty(urlRegex)) {
                                        route.UrlRegex = baseUrl;
                                    } else {
                                        route.UrlRegex = $"{baseUrl}/{urlRegex}";
                                    }
                                    route.SubPaths = route.UrlRegex.Count(c => c == '/');
                                    route.RouteOwner = routeManager;

                                    route.Callable = (HttpRequest request) => {
                                        Route r = route;
                                        string path = request.Path;
                                        string[] subPaths = path.Split('/');

                                        List<object> objs = new List<object>();
                                        objs.Add(request);
                                        for (int z = route.SubPaths + 1; z < subPaths.Length; z++) {
                                            string sub = subPaths[u];
                                            objs.Add(sub);
                                        }

                                        return (HttpResponse)method.Invoke(routeManager, objs.ToArray());
                                    };
                                    routes.Add(route);

                                    ConsoleU.WriteLine($"   Route: [{route.Method}] /{urlRegex}", ConsoleColor.Yellow);
                                }
                            }

                        }

                    }
                }
            }

            return routes;
        }
    }
}
