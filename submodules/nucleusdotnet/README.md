# NucleusDotNET
NucleusDotNET is a cross-platform library used for the development of .NET applications
It compiles under .NET Framework 4 (Client) or .NET Core 3

# Features
* Multi-Threading
Task Manager system that delegates tasks and can have Delegate rules for the execution of next tasks.
    - If your task needs another task to complete, you can queue it
    - Tasks that are returned without completion are re-scheduled to re-run when all dependent tasks have finished
    - No sample code for the moment.
    - Initially designed for the rendering of video, as some scenes on my system had to be rendered in frame-order to not mess up the scrubbing


* Web Server with routing
    - Sample: https://github.com/lucasassislar/distrolucasblog
    - Regex route system - Make a base class for all your page routes and add a "[RouteManager("^/w")]" attribute
        - Route functions: [Route("GET", "path_to_route_regex")]
        - Parameters passed to the route will be input to the function, so this works:
            [Route("GET", "/path")]
            public HttpResponse CustomPage(HttpRequest request, string pageName, string language)
            (TO-DO: actually declare the parameters on the route, right now we just receive and pass all we can forward, which could be a potential security hazard)


* TaskApp (WIP)
    - Application that you give tasks and it executes them on a parent-less process

* MFT scan
    - Unsafe code but it works wonders.
    - If the storage is NTFS, you can scan the Master File Table to find files inside the user's disk incredibly fast (like 5 seconds to list all *.exe files)

* Profiling
    - VERY Simple but fast system to benchmark/measure the time needed to finish tasks
    
* Console Engine
    - Used to quickly build console commands, and automatically register them on the application.


# Projects Using NucleusDotNet

- https://github.com/lucasassislar/distrolucasblog 
My personal website uses the library for serving and routing on a Linux machine by running it under .NET Core

- https://github.com/lucasassislar/nucleuscoop 
Nucleus Coop uses it for the UI and most split functions/Win32 access

- Videofy (WIP no link)
It's a Video creation tool with the renderer designed in C#
