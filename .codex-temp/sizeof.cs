using System;
using System.Runtime.InteropServices;
using helengine;
using helengine.directx11;

Console.WriteLine($"StandardMeshShaderData={Marshal.SizeOf<StandardMeshShaderData>()}");
Console.WriteLine($"DirectX11ForwardLightSlotShaderData={Marshal.SizeOf<DirectX11ForwardLightSlotShaderData>()}");
Console.WriteLine($"DirectX11ForwardLightShaderData={Marshal.SizeOf<DirectX11ForwardLightShaderData>()}");
Console.WriteLine($"DirectX11ShadowShaderData={Marshal.SizeOf<DirectX11ShadowShaderData>()}");
