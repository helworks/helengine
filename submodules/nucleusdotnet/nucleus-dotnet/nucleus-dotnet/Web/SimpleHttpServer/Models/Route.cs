// Copyright (C) 2016 by Barend Erasmus and donated to the public domain

using System;

namespace Nucleus.Web {
    public class Route {
        public string Name { get; set; } // descriptive name for debugging
        public int SubPaths { get; set; }

        public string UrlRegex { get; set; }
        public string Method { get; set; }
        public Func<HttpRequest, HttpResponse> Callable { get; set; }

        public object RouteOwner { get; set; }

        public override string ToString() {
            return $"[{Method}] {UrlRegex}";
        }
    }
}
