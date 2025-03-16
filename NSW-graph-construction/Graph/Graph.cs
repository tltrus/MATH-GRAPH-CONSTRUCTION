using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace MathGraph
{
    class Graph
    {
        public delegate void MessageHandler(string msg);
        public event MessageHandler MessageNotify;

        public class Node
        {
            public Point pos;
            public int r = 5; // radius
            public int label;
            public int id;
            public int radar = 120;

            public Node(int x, int y, int label, int id)
            {
                pos = new Point(x, y);
                this.label = label;
                this.id = id;
            }
        }
        public List<Node> nodes;
        public class Edge { public int from, to; public double dist; }
        public List<Edge> edges;
        public int nodes_num;
        private Random rnd;
        private int width, height;
        private int state;
        private int counter;

        public Graph(Random random, int width, int height)
        {
            nodes = new List<Node>();
            edges = new List<Edge>();
            rnd = random;
            this.width = width;
            this.height = height;
        }
        private void Init()
        {
            nodes.Clear();
            edges.Clear();
        }

        // ADD NODE
        private void AddNode(int i, int x = 0, int y = 0)
        {
            // New node
            if (x == 0) x = rnd.Next(10, width - 10);
            if (y == 0) y = rnd.Next(10, height - 10);
            Node new_node = new Node(x, y, 0, i);

            // New edge
            List<Dist> dists = new List<Dist>();
            for (int j = 0; j < nodes.Count; ++j)
            {
                var dist = GetDist(nodes[j].pos, new_node.pos);
                dists.Add(new Dist() { node = j, dist = dist });
            }
            var sorted = dists.OrderBy(d => d.dist, new CustomDoubleComparer()).Take(2).ToList();
            foreach(var s in sorted)
            {
                edges.Add(new Edge { from = i, to = s.node, dist = s.dist});
            }

            // Add new node
            nodes.Add(new_node);
            MessageNotify?.Invoke("\nAdd node " + i);
        }
        public void ConstructionStatic(int nodes_num)
        {
            Init();
            this.nodes_num = nodes_num;

            for (int i = 0; i < nodes_num; ++i)
            {
                AddNode(i);
            }
        }
        public void ConstructionDynamic(int nodes_num)
        {
            switch(state)
            {
                case 0: // init
                    this.nodes_num = nodes_num;
                    Init();
                    state = 1;

                    break;

                case 1: // calculation
                    if (counter >= nodes_num) break;
                    AddNode(counter);
                    counter++;

                    break;
            }
        }
        private double GetDist(Point p1, Point p2) => Math.Sqrt((p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y));

        // DRAWING
        public void Draw(DrawingContext dc)
        {
            // Draw connections
            for (int i = 0; i < edges.Count; ++i)
            {
                Point a = nodes[edges[i].from].pos;
                Point b = nodes[edges[i].to].pos;
                dc.DrawLine(new Pen(Brushes.DarkGray, 0.7), a, b);
            }

            // Draw nodes
            for (int i = 0; i < nodes.Count; ++i)
            {
                // Draw point
                var p = nodes[i].pos;
                var r = nodes[i].r;
                Brush brush = null;
                switch (nodes[i].label)
                {
                    case 0:
                        brush = Brushes.DeepSkyBlue;
                        break;
                    case 1:
                        brush = Brushes.Green;
                        break;
                }
                dc.DrawEllipse(brush, null, p, r, r);

                // Draw labeling
                FormattedText formattedText = new FormattedText(i.ToString(), CultureInfo.GetCultureInfo("en-us"),
                                                                FlowDirection.LeftToRight, new Typeface("Verdana"), 7, Brushes.Black,
                                                                VisualTreeHelper.GetDpi(MainWindow.visual).PixelsPerDip);
                dc.DrawText(formattedText, new Point(p.X + 5, p.Y - r - 15));
            }
        }
    }
    public class Dist
    {
        public int node;
        public double dist;
    }

    class CustomDoubleComparer : IComparer<double>
    {
        public int Compare(double a, double b)
        {
            if (a > b)
                return 1;
            else if (a == b)
                return 0;
            else
                return -1;
        }
    }
}
