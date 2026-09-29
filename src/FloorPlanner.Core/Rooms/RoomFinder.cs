using FloorPlanner.Core.Geometry;
using FloorPlanner.Core.Model;

namespace FloorPlanner.Core.Rooms;

/// <summary>
/// Finds rooms as the bounded faces of the planar graph formed by the walls.
/// Corners are wall endpoints; a wall is split wherever another wall ends on it (T-junctions),
/// so one long wall with a dividing wall against it yields two rooms.
/// </summary>
public static class RoomFinder
{
    // Faces smaller than this are slivers from float noise, or a lone wall walked on both sides.
    private const double MinArea = 10_000; // 0.01 m²

    public static IReadOnlyList<Room> Find(IEnumerable<Wall> walls)
    {
        var graph = Graph.Build(walls.Where(w => w.Length > BuildRules.MergeTolerance).ToList());
        var rooms = new List<Room>();
        var visited = new HashSet<(int From, int To)>();

        foreach (var start in graph.HalfEdges())
        {
            if (visited.Contains(start)) continue;

            var face = new List<int>();
            var edge = start;
            do
            {
                visited.Add(edge);
                face.Add(edge.From);
                edge = (edge.To, graph.NextAround(edge.To, cameFrom: edge.From));
            } while (edge != start);

            var outline = face.Select(i => graph.Vertices[i]).ToList();
            var signedArea = SignedArea(outline);

            // Walking "always take the next edge clockwise" traces bounded faces with positive area
            // and the outside of each connected group of walls with negative area.
            if (signedArea > MinArea) rooms.Add(new Room(outline, signedArea, Centroid(outline, signedArea)));
        }
        return rooms;
    }

    private static double SignedArea(IReadOnlyList<Vec2> polygon)
    {
        var sum = 0.0;
        for (var i = 0; i < polygon.Count; i++)
        {
            var (a, b) = (polygon[i], polygon[(i + 1) % polygon.Count]);
            sum += a.X * b.Y - b.X * a.Y;
        }
        return sum / 2;
    }

    private static Vec2 Centroid(IReadOnlyList<Vec2> polygon, double signedArea)
    {
        double cx = 0, cy = 0;
        for (var i = 0; i < polygon.Count; i++)
        {
            var (a, b) = (polygon[i], polygon[(i + 1) % polygon.Count]);
            var cross = a.X * b.Y - b.X * a.Y;
            cx += (a.X + b.X) * cross;
            cy += (a.Y + b.Y) * cross;
        }
        return new Vec2(cx, cy) / (6 * signedArea);
    }

    private sealed class Graph
    {
        public List<Vec2> Vertices { get; } = [];

        // Neighbours of each vertex, sorted by direction angle.
        private readonly List<List<int>> _neighbours = [];

        public static Graph Build(IReadOnlyList<Wall> walls)
        {
            var graph = new Graph();
            foreach (var wall in walls)
            {
                graph.VertexAt(wall.Start);
                graph.VertexAt(wall.End);
            }

            var edges = new HashSet<(int, int)>();
            foreach (var wall in walls)
            {
                // Every corner lying on this wall, ordered from start to end.
                var direction = wall.End - wall.Start;
                var onWall = Enumerable.Range(0, graph.Vertices.Count)
                    .Where(i => GeometryMath.DistanceToSegment(graph.Vertices[i], wall.Start, wall.End) <= BuildRules.MergeTolerance)
                    .OrderBy(i => (graph.Vertices[i] - wall.Start).Dot(direction))
                    .ToList();

                foreach (var (a, b) in onWall.Zip(onWall.Skip(1)))
                    edges.Add((Math.Min(a, b), Math.Max(a, b)));
            }

            foreach (var (a, b) in edges)
            {
                graph._neighbours[a].Add(b);
                graph._neighbours[b].Add(a);
            }
            for (var i = 0; i < graph.Vertices.Count; i++)
            {
                var origin = graph.Vertices[i];
                graph._neighbours[i].Sort((p, q) => Angle(origin, graph.Vertices[p]).CompareTo(Angle(origin, graph.Vertices[q])));
            }
            return graph;
        }

        public IEnumerable<(int From, int To)> HalfEdges() =>
            _neighbours.SelectMany((list, from) => list.Select(to => (from, to)));

        /// <summary>Arriving at <paramref name="vertex"/> from <paramref name="cameFrom"/>, the next edge clockwise.</summary>
        public int NextAround(int vertex, int cameFrom)
        {
            var list = _neighbours[vertex];
            var index = list.IndexOf(cameFrom);
            return list[(index - 1 + list.Count) % list.Count];
        }

        private int VertexAt(Vec2 p)
        {
            var existing = Vertices.FindIndex(v => v.DistanceTo(p) <= BuildRules.MergeTolerance);
            if (existing >= 0) return existing;

            Vertices.Add(p);
            _neighbours.Add([]);
            return Vertices.Count - 1;
        }

        private static double Angle(Vec2 from, Vec2 to) => Math.Atan2(to.Y - from.Y, to.X - from.X);
    }
}
