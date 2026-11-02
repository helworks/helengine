#pragma once
#include "Polygon.h"
#include "Random.h"
#include "Triangle.h"
namespace hellm::detail {
/// <summary>Evaluate a directed atlas edge with double-precision intersections.</summary>
double Edge(Point2 a, Point2 b, Point2 p);
/// <summary>Clip a convex polygon against a directed half-plane without quantizing new vertices.</summary>
Polygon Clip(const Polygon &input, Point2 a, Point2 b);
/// <summary>Orient the original UV triangle counterclockwise for exact clipping.</summary>
Polygon TrianglePolygon(const Triangle &triangle);
/// <summary>Intersect the triangle with a full-resolution texel square.</summary>
Polygon PixelPolygon(const Triangle &triangle, uint32_t x, uint32_t y);
/// <summary>Measure clipped polygon area using a triangle fan.</summary>
double Area(const Polygon &polygon);
/// <summary>Measure positive overlap while preserving exact zero-area shared boundaries.</summary>
double Overlap(const Triangle &a, const Triangle &b);
/// <summary>Draw an area-uniform point from a clipped texel polygon.</summary>
Point2 SamplePolygon(const Polygon &polygon, Random &random);
/// <summary>Recover original-corner weights without quantizing the sampled atlas location.</summary>
Vec2 Barycentric(const Triangle &triangle, Point2 uv);
} // namespace hellm::detail
