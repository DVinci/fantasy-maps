using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Voronoi;

var polygon = new Polygon();
polygon.Add(new Vertex(-0.4, -0.4));
polygon.Add(new Vertex( 0.4, -0.4));
polygon.Add(new Vertex( 0.4,  0.4));
polygon.Add(new Vertex(-0.4,  0.4));
polygon.Add(new Vertex(0, 0));
polygon.Add(new Vertex(0.2, 0.1));
polygon.Add(new Vertex(-0.1, 0.3));

var delaunay = (TriangleNet.Mesh)polygon.Triangulate(new ConstraintOptions(), new QualityOptions());
var voronoi = new BoundedVoronoi(delaunay);

Console.WriteLine("Vertex count: " + voronoi.Vertices.Count);
Console.WriteLine("HalfEdge count: " + voronoi.HalfEdges.Count);
Console.WriteLine("Face count: " + voronoi.Faces.Count);

var he = voronoi.HalfEdges[0];
Console.WriteLine("HalfEdge props:");
foreach (var p in he.GetType().GetProperties())
    Console.WriteLine("  " + p.Name + " : " + p.PropertyType.Name);

var face = voronoi.Faces[0];
Console.WriteLine("Face props:");
foreach (var p in face.GetType().GetProperties())
    Console.WriteLine("  " + p.Name + " : " + p.PropertyType.Name);

var vertex = voronoi.Vertices[0];
Console.WriteLine("Vertex props:");
foreach (var p in vertex.GetType().GetProperties())
    Console.WriteLine("  " + p.Name + " : " + p.PropertyType.Name);
