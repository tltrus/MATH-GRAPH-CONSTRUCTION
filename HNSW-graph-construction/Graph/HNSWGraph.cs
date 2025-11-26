using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Globalization;
using System.Windows.Threading;

namespace MathGraph
{
    class HNSWGraph
    {
        public delegate void MessageHandler(string msg);
        public event MessageHandler MessageNotify;

        public class Node
        {
            public Point Position;
            public int Radius = 4;
            public int Id;
            public int Layer;
            public bool IsVisited;
            public bool IsInPath;

            public Node(int id, Point position, int layer)
            {
                Id = id;
                Position = position;
                Layer = layer;
            }
        }

        public class Edge
        {
            public int From, To;
            public double Distance;
            public int Layer;
        }

        private Random rnd;
        private int width, height;
        private DispatcherTimer timer;

        public List<Node> Nodes { get; private set; }
        public List<Edge> Edges { get; private set; }
        public List<List<int>> Layers { get; private set; }

        private Point? queryPoint = null;
        private List<int> searchPath;
        private int currentNodeId = -1;
        private int entryPointId = -1;

        // HNSW parameters
        public int LayerCount { get; private set; } = 7;
        private int M = 5; // number of neighbors
        private double mL;
        private int buildStep = 0;

        public bool IsBuildComplete { get; private set; } = false;

        public HNSWGraph(Random random, int width, int height)
        {
            rnd = random;
            this.width = width;
            this.height = height;

            Nodes = new List<Node>();
            Edges = new List<Edge>();
            Layers = new List<List<int>>();
            searchPath = new List<int>();

            mL = 1 / Math.Log(M);

            // Initialize layers
            for (int i = 0; i < LayerCount; i++)
            {
                Layers.Add(new List<int>());
            }

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += Timer_Tick;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!IsBuildComplete && buildStep < Nodes.Capacity)
            {
                BuildStepByStep();
            }
        }

        public void Reset()
        {
            Nodes.Clear();
            Edges.Clear();
            searchPath.Clear();
            foreach (var layer in Layers) layer.Clear();
            queryPoint = null;
            buildStep = 0;
            entryPointId = -1;
            currentNodeId = -1;
            IsBuildComplete = false;
        }

        public void BuildComplete(int nodeCount)
        {
            Reset();
            Nodes.Capacity = nodeCount;
            for (int i = 0; i < nodeCount; i++)
            {
                AddNode(i);
            }
            IsBuildComplete = true;
            MessageNotify?.Invoke("\nHNSW construction complete!");
        }

        public void BuildStepByStep(int nodeCount)
        {
            if (buildStep == 0)
            {
                Nodes.Capacity = nodeCount;
            }

            if (buildStep >= nodeCount)
            {
                IsBuildComplete = true;
                timer.Stop();
                MessageNotify?.Invoke("\nHNSW construction complete!");
                return;
            }

            AddNode(buildStep);
            buildStep++;

            if (buildStep % 10 == 0 || buildStep == nodeCount)
            {
                MessageNotify?.Invoke($"\nAdded node {buildStep}/{nodeCount}");
            }
        }

        private void BuildStepByStep()
        {
            if (buildStep >= Nodes.Capacity)
            {
                IsBuildComplete = true;
                timer.Stop();
                MessageNotify?.Invoke("\nHNSW construction complete!");
                return;
            }

            AddNode(buildStep);
            buildStep++;

            if (buildStep % 10 == 0 || buildStep == Nodes.Capacity)
            {
                MessageNotify?.Invoke($"\nAdded node {buildStep}/{Nodes.Capacity}");
            }
        }

        private void AddNode(int nodeId)
        {
            Point position = new Point(rnd.Next(20, width - 20), rnd.Next(20, height - 20));
            int layer = GetRandomLayer();

            Node newNode = new Node(nodeId, position, layer);
            Nodes.Add(newNode);
            Layers[layer].Add(nodeId);

            if (nodeId == 0)
            {
                entryPointId = nodeId;
                return;
            }

            // Insert node into HNSW
            int currentMaxLayer = GetMaxLayer();
            int ep = entryPointId;

            // Search from top layers down to the new node's layer + 1
            for (int lc = currentMaxLayer; lc > layer; lc--)
            {
                ep = SearchAtLayer(ep, newNode.Position, lc);
            }

            // Connect the new node at each layer from its layer down to 0
            for (int lc = Math.Min(layer, currentMaxLayer); lc >= 0; lc--)
            {
                var neighbors = SearchAtLayerWithNeighbors(ep, newNode.Position, lc, M);

                // Connect to neighbors
                foreach (int neighborId in neighbors)
                {
                    AddEdge(nodeId, neighborId, lc);
                    AddEdge(neighborId, nodeId, lc); // Bidirectional connection
                }

                // Limit connections to prevent overgrowth
                PruneConnections(nodeId, lc, M * 2);
            }

            // Update entry point if new node is at a higher layer
            if (layer > GetNode(entryPointId).Layer)
            {
                entryPointId = nodeId;
            }
        }

        public void SearchShortestPath()
        {
            if (queryPoint == null || Nodes.Count == 0) return;

            // Reset node states
            foreach (var node in Nodes)
            {
                node.IsVisited = false;
                node.IsInPath = false;
            }
            searchPath.Clear();

            Point target = (Point)queryPoint;

            // Use greedy search from entry point to find closest node to q
            int currentId = entryPointId;
            searchPath.Add(currentId);
            GetNode(currentId).IsInPath = true;

            double currentDist = GetDistance(GetNode(currentId).Position, target);
            bool improved = true;

            while (improved)
            {
                improved = false;
                var neighbors = GetNeighborsAtLayer(currentId, 0); // Search at base layer

                // Find the neighbor that minimizes distance to target
                foreach (int neighborId in neighbors)
                {
                    if (searchPath.Contains(neighborId)) continue;

                    double neighborDist = GetDistance(GetNode(neighborId).Position, target);
                    if (neighborDist < currentDist)
                    {
                        currentDist = neighborDist;
                        currentId = neighborId;
                        improved = true;
                        break; // Greedy - take first improvement
                    }
                }

                if (improved)
                {
                    searchPath.Add(currentId);
                    GetNode(currentId).IsInPath = true;
                }
            }

            // Print path to console
            string pathString = string.Join(" → ", searchPath);
            MessageNotify?.Invoke($"\nShortest path from EP: {pathString} ");
            MessageNotify?.Invoke($"Closest node: {currentId}, Distance: {currentDist:F2}");
        }

        private int SearchAtLayer(int entryPointId, Point target, int layer)
        {
            if (Nodes.Count == 0) return -1;

            var visited = new HashSet<int>();
            var candidates = new List<int> { entryPointId };
            var bestNode = entryPointId;
            double bestDistance = GetDistance(GetNode(entryPointId).Position, target);

            while (candidates.Count > 0)
            {
                int currentId = candidates[0];
                candidates.RemoveAt(0);

                if (visited.Contains(currentId)) continue;
                visited.Add(currentId);

                double currentDistance = GetDistance(GetNode(currentId).Position, target);
                if (currentDistance < bestDistance)
                {
                    bestDistance = currentDistance;
                    bestNode = currentId;
                }

                // Get neighbors at this layer
                var neighbors = GetNeighborsAtLayer(currentId, layer);
                foreach (int neighborId in neighbors)
                {
                    if (!visited.Contains(neighborId))
                    {
                        candidates.Add(neighborId);
                    }
                }

                // Sort by distance to target
                candidates = candidates.OrderBy(id => GetDistance(GetNode(id).Position, target)).ToList();

                // Keep only top candidates
                if (candidates.Count > M)
                {
                    candidates = candidates.Take(M).ToList();
                }
            }

            return bestNode;
        }

        private List<int> SearchAtLayerWithNeighbors(int entryPointId, Point target, int layer, int ef)
        {
            var results = new List<int>();
            if (Nodes.Count == 0) return results;

            var visited = new HashSet<int>();
            var candidates = new List<int> { entryPointId };
            results.Add(entryPointId);

            while (candidates.Count > 0)
            {
                int currentId = candidates[0];
                candidates.RemoveAt(0);

                if (visited.Contains(currentId)) continue;
                visited.Add(currentId);

                // Get neighbors at this layer
                var neighbors = GetNeighborsAtLayer(currentId, layer);
                foreach (int neighborId in neighbors)
                {
                    if (!visited.Contains(neighborId) && !candidates.Contains(neighborId))
                    {
                        candidates.Add(neighborId);
                        results.Add(neighborId);
                    }
                }

                // Sort by distance
                candidates = candidates.OrderBy(id => GetDistance(GetNode(id).Position, target)).ToList();
                results = results.OrderBy(id => GetDistance(GetNode(id).Position, target)).ToList();

                // Keep only top ef
                if (results.Count > ef)
                {
                    results = results.Take(ef).ToList();
                }
                if (candidates.Count > ef)
                {
                    candidates = candidates.Take(ef).ToList();
                }
            }

            return results.Take(ef).ToList();
        }

        public void Search()
        {
            if (queryPoint == null || Nodes.Count == 0) return;

            // Reset node states
            foreach (var node in Nodes)
            {
                node.IsVisited = false;
                node.IsInPath = false;
            }
            searchPath.Clear();

            int ep = entryPointId;
            Point target = (Point)queryPoint;

            // Search from top layer to bottom
            for (int lc = LayerCount - 1; lc >= 1; lc--)
            {
                ep = SearchAtLayer(ep, target, lc);
            }

            // Detailed search at layer 0 with visualization
            currentNodeId = SearchAtLayerDetailed(ep, target, 0);

            MessageNotify?.Invoke($"\nSearch complete! Closest node: {currentNodeId}, Distance: {GetDistance(GetNode(currentNodeId).Position, target):F2}");
        }

        private int SearchAtLayerDetailed(int entryPointId, Point target, int layer)
        {
            var visited = new HashSet<int>();
            var candidates = new List<int> { entryPointId };
            int bestNode = entryPointId;
            double bestDistance = GetDistance(GetNode(entryPointId).Position, target);

            searchPath.Add(entryPointId);
            GetNode(entryPointId).IsInPath = true;

            while (candidates.Count > 0)
            {
                int currentId = candidates[0];
                candidates.RemoveAt(0);

                if (visited.Contains(currentId)) continue;
                visited.Add(currentId);

                GetNode(currentId).IsVisited = true;

                double currentDistance = GetDistance(GetNode(currentId).Position, target);
                if (currentDistance < bestDistance)
                {
                    bestDistance = currentDistance;
                    bestNode = currentId;
                }

                // Get neighbors
                var neighbors = GetNeighborsAtLayer(currentId, layer);
                foreach (int neighborId in neighbors)
                {
                    if (!visited.Contains(neighborId) && !candidates.Contains(neighborId))
                    {
                        candidates.Add(neighborId);
                    }
                }

                // Sort by distance
                candidates = candidates.OrderBy(id => GetDistance(GetNode(id).Position, target)).ToList();

                // Visualize search path
                if (candidates.Count > 0 && !searchPath.Contains(candidates[0]))
                {
                    searchPath.Add(candidates[0]);
                    GetNode(candidates[0]).IsInPath = true;
                }
            }

            return bestNode;
        }

        private List<int> GetNeighborsAtLayer(int nodeId, int layer)
        {
            return Edges
                .Where(e => e.From == nodeId && e.Layer == layer)
                .Select(e => e.To)
                .Distinct()
                .ToList();
        }

        private void AddEdge(int fromId, int toId, int layer)
        {
            if (fromId == toId) return;

            // Check if edge already exists
            if (Edges.Any(e => e.From == fromId && e.To == toId && e.Layer == layer))
                return;

            double distance = GetDistance(GetNode(fromId).Position, GetNode(toId).Position);
            Edges.Add(new Edge { From = fromId, To = toId, Distance = distance, Layer = layer });
        }

        private void PruneConnections(int nodeId, int layer, int maxConnections)
        {
            var connections = Edges.Where(e => e.From == nodeId && e.Layer == layer).ToList();
            if (connections.Count <= maxConnections) return;

            var nodePos = GetNode(nodeId).Position;
            var sortedConnections = connections
                .OrderBy(e => GetDistance(nodePos, GetNode(e.To).Position))
                .Take(maxConnections)
                .ToList();

            // Remove excess connections
            Edges.RemoveAll(e => e.From == nodeId && e.Layer == layer);
            Edges.AddRange(sortedConnections);
        }

        private int GetRandomLayer() 
        {
            double r = -Math.Log(rnd.NextDouble()) * mL;
            return Math.Min((int)r, LayerCount - 1);
        }

        private int GetMaxLayer() => Nodes.Count > 0 ? Nodes.Max(n => n.Layer) : 0;

        private Node GetNode(int id) => Nodes.FirstOrDefault(n => n.Id == id);

        private double GetDistance(Point p1, Point p2) => Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

        public void SetQueryPoint(Point point) => queryPoint = point;

        public void StartBuilding(int nodeCount)
        {
            Reset();
            Nodes.Capacity = nodeCount;
            timer.Start();
        }

        public void StopBuilding() => timer.Stop();

        public void Draw(DrawingContext dc)
        {
            // Draw edges with different colors for different layers
            foreach (var edge in Edges)
            {
                Point from = GetNode(edge.From).Position;
                Point to = GetNode(edge.To).Position;

                Color color = Colors.LightGray;
                double thickness = 0.5;

                if (edge.Layer == 0)
                {
                    color = Colors.LightGray;
                    thickness = 0.5;
                }
                else if (edge.Layer == 1)
                {
                    color = Colors.Gray;
                    thickness = 0.7;
                }
                else if (edge.Layer == 2)
                {
                    color = Colors.DarkGray;
                    thickness = 0.9;
                }
                else if (edge.Layer >= 3)
                {
                    color = Colors.DimGray;
                    thickness = 1.1;
                }

                dc.DrawLine(new Pen(new SolidColorBrush(color), thickness), from, to);
            }

            // Draw search path (only the short greedy path)
            if (searchPath.Count > 1)
            {
                for (int i = 0; i < searchPath.Count - 1; i++)
                {
                    Point from = GetNode(searchPath[i]).Position;
                    Point to = GetNode(searchPath[i + 1]).Position;

                    // Draw thick green line for search path
                    dc.DrawLine(new Pen(Brushes.Green, 4), from, to);

                    // Draw direction arrows
                    DrawArrow(dc, from, to, Brushes.DarkGreen);
                }
            }

            // Draw nodes with size and color intensity based on layer
            foreach (var node in Nodes)
            {
                // Calculate size based on layer (higher layer = bigger)
                double radius = 4 + (node.Layer * 2);

                // Calculate blue color intensity based on layer
                byte blueIntensity = (byte)(100 + (node.Layer * 50));
                Color blueColor = Color.FromRgb(0, 0, blueIntensity);

                Brush brush = new SolidColorBrush(blueColor);

                // Highlight nodes in search path
                if (node.IsInPath)
                {
                    if (node.Id == searchPath[0])
                        brush = Brushes.Purple; // Entry point
                    else if (node.Id == searchPath[searchPath.Count - 1])
                        brush = Brushes.Red; // Closest node
                    else
                        brush = Brushes.Orange; // Intermediate nodes

                    radius += 1; // Make path nodes larger
                }

                // Draw the node
                dc.DrawEllipse(brush, null, node.Position, radius, radius);

                // Draw subtle border
                dc.DrawEllipse(null, new Pen(Brushes.Black, 0.5), node.Position, radius, radius);

                // Draw node ID and layer info
                FormattedText text = new FormattedText(
                    $"{node.Id}(L{node.Layer})",
                    CultureInfo.GetCultureInfo("en-us"),
                    FlowDirection.LeftToRight,
                    new Typeface("Verdana"),
                    6,
                    Brushes.Black,
                    VisualTreeHelper.GetDpi(MainWindow.visual).PixelsPerDip);

                dc.DrawText(text, new Point(node.Position.X - 8, node.Position.Y - radius - 10));
            }

            // Draw query point
            if (queryPoint != null)
            {
                dc.DrawEllipse(Brushes.Red, null, (Point)queryPoint, 6, 6);
                dc.DrawEllipse(null, new Pen(Brushes.DarkRed, 2), (Point)queryPoint, 8, 8);

                // Label query point
                FormattedText qText = new FormattedText(
                    "Q",
                    CultureInfo.GetCultureInfo("en-us"),
                    FlowDirection.LeftToRight,
                    new Typeface("Verdana"),
                    7,
                    Brushes.DarkRed,
                    VisualTreeHelper.GetDpi(MainWindow.visual).PixelsPerDip);
                dc.DrawText(qText, new Point(((Point)queryPoint).X + 10, ((Point)queryPoint).Y - 5));
            }
        }

        // Helper method to draw arrows
        private void DrawArrow(DrawingContext dc, Point from, Point to, Brush brush)
        {
            Vector direction = to - from;
            direction.Normalize();

            Vector perpendicular = new Vector(-direction.Y, direction.X) * 3;

            Point arrowHead1 = to - direction * 10 + perpendicular;
            Point arrowHead2 = to - direction * 10 - perpendicular;

            dc.DrawLine(new Pen(brush, 2), arrowHead1, to);
            dc.DrawLine(new Pen(brush, 2), arrowHead2, to);
        }
        private void DrawLegend(DrawingContext dc)
        {
            double legendX = 10;
            double legendY = 10;

            FormattedText legendTitle = new FormattedText(
                "HNSW Layers Legend:",
                CultureInfo.GetCultureInfo("en-us"),
                FlowDirection.LeftToRight,
                new Typeface("Verdana"),
                9,
                Brushes.Black,
                VisualTreeHelper.GetDpi(MainWindow.visual).PixelsPerDip);
            dc.DrawText(legendTitle, new Point(legendX, legendY));

            // Draw layer examples
            for (int layer = 0; layer < LayerCount; layer++)
            {
                double radius = 4 + (layer * 2);
                byte blueIntensity = (byte)(100 + (layer * 50));
                Color blueColor = Color.FromRgb(0, 0, blueIntensity);

                double yPos = legendY + 20 + (layer * 20);

                // Draw example node
                dc.DrawEllipse(new SolidColorBrush(blueColor), null,
                    new Point(legendX + 10, yPos), radius, radius);

                // Draw layer text
                FormattedText layerText = new FormattedText(
                    $"Layer {layer}: Size={radius}, Intensity={blueIntensity}",
                    CultureInfo.GetCultureInfo("en-us"),
                    FlowDirection.LeftToRight,
                    new Typeface("Verdana"),
                    8,
                    Brushes.Black,
                    VisualTreeHelper.GetDpi(MainWindow.visual).PixelsPerDip);
                dc.DrawText(layerText, new Point(legendX + 25, yPos - 5));
            }

            // Draw search status legend
            double statusY = legendY + 20 + (LayerCount * 20);

            FormattedText statusTitle = new FormattedText(
                "Search Status:",
                CultureInfo.GetCultureInfo("en-us"),
                FlowDirection.LeftToRight,
                new Typeface("Verdana"),
                9,
                Brushes.Black,
                VisualTreeHelper.GetDpi(MainWindow.visual).PixelsPerDip);
            dc.DrawText(statusTitle, new Point(legendX, statusY));

            dc.DrawEllipse(Brushes.Green, null, new Point(legendX + 15, statusY + 20), 4, 4);
            FormattedText pathText = new FormattedText("Current Path",
                CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight,
                new Typeface("Verdana"), 8, Brushes.Black, VisualTreeHelper.GetDpi(MainWindow.visual).PixelsPerDip);
            dc.DrawText(pathText, new Point(legendX + 25, statusY + 16));

            dc.DrawEllipse(Brushes.LightGreen, null, new Point(legendX + 15, statusY + 40), 4, 4);
            FormattedText visitedText = new FormattedText("Visited Nodes",
                CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight,
                new Typeface("Verdana"), 8, Brushes.Black, VisualTreeHelper.GetDpi(MainWindow.visual).PixelsPerDip);
            dc.DrawText(visitedText, new Point(legendX + 25, statusY + 36));

            dc.DrawEllipse(Brushes.Purple, null, new Point(legendX + 15, statusY + 60), 4, 4);
            FormattedText epText = new FormattedText("Entry Point (EP)",
                CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight,
                new Typeface("Verdana"), 8, Brushes.Black, VisualTreeHelper.GetDpi(MainWindow.visual).PixelsPerDip);
            dc.DrawText(epText, new Point(legendX + 25, statusY + 56));
        }
    }
}