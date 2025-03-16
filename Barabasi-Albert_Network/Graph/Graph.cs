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

            public Node(int x, int y)
            {
                pos = new Point(x, y);
            }
        }
        public List<Node> nodes;
        public class Edge
        {
            public int from, to;
        }
        public List<Edge> connections;
        int m_parameter = 3; // value of m parameter (m<=m_0)

        public int number_of_nodes
        {
            get { return nodes.Count; }
        }

        private Random rnd;
        private int width, height;
        int new_node;

        public Graph(Random random, int width, int height)
        {
            nodes = new List<Node>();
            connections = new List<Edge>();
            rnd = random;
            this.width = width;
            this.height = height;
            new_node = 0;
        }
        public void Initialization(int nodes_num = 1, int m_param = 3)
        {
            m_parameter = m_param;

            nodes.Clear();
            connections.Clear();

            for (int i = 0; i < nodes_num; ++i)
            {
                new_node = i;

                AddNode();

                for (int j = 0; j < m_parameter; ++j)
                    AddEdge();
            }
        }

        public void AddPoint(Point point)
        {
            new_node++;

            AddNode((int)point.X, (int)point.Y);

            for (int j = 0; j < m_parameter; ++j)
                AddEdge();
        }
        private void AddNode(int x = 0, int y = 0)
        {
            if (x == 0) 
                x = rnd.Next(10, width - 10);
            if (y == 0)
                y = rnd.Next(10, height - 10);

            Node node = new Node(x, y);
            nodes.Add(node);
        }
        private void AddEdge()
        {
            int rand_node = -1;

            if (connections.Count == 0)
            {
                rand_node = 0;
            }
            else
            {
                while (rand_node == -1)
                {
                    rand_node = GetRandomNode();
                }
            }
            if (new_node == rand_node) return;

            bool isEdgeInGraph = CheckEdgeInGraph(new_node, rand_node);
            if (!isEdgeInGraph)
            {
                Edge edge = new Edge() { from = new_node, to = rand_node };
                connections.Add(edge);
                edge = new Edge() { from = rand_node, to = new_node };
                connections.Add(edge);

                MessageNotify?.Invoke("\nEdge added: " + new_node + " <--> " + rand_node);
            }
        }
        private bool CheckEdgeInGraph(int node1, int node2)
        {
            int count = connections.Where(a => a.from == node1 && a.to == node2).Count();
            if (count > 0)
                return true;
            else
                return false;
        }
        private int GetRandomNode()
        {
            var nodes_degr = connections.Count; // Get nodes degree

            List<Prob> p_list = new List<Prob>();
            for (int i = 0; i < number_of_nodes; ++i)
            {
                double node_k = GetDegree(i);
                double p = node_k / nodes_degr;

                p_list.Add(new Prob() { id = i, value = p });
            }

            for (int i = 0; i < p_list.Count; ++i)
            {
                var rand = rnd.NextDouble();
                if (rand < p_list[i].value)
                    return p_list[i].id;
            }
            return -1;
        }
        private int GetDegree(int n)
        {
            int result = connections.Where(a => a.from == n).Count();
            return result;
        }
        public void Draw(DrawingContext dc)
        {
            // Draw connections
            for (int i = 0; i < connections.Count; ++i)
            {
                Point a = nodes[connections[i].from].pos;
                Point b = nodes[connections[i].to].pos;
                dc.DrawLine(new Pen(Brushes.LightSkyBlue, 0.5), a, b);
            }

            // Draw nodes
            for (int i = 0; i < nodes.Count; ++i)
            {
                // Draw point
                var p = nodes[i].pos;
                var pen = new Pen(Brushes.DeepSkyBlue, 1);
                var r = nodes[i].r;
                dc.DrawEllipse(Brushes.LightSkyBlue, pen, p, r, r);

                // Draw labeling
                FormattedText formattedText = new FormattedText(i.ToString(), CultureInfo.GetCultureInfo("en-us"),
                                                                FlowDirection.LeftToRight, new Typeface("Verdana"), 8, Brushes.Black,
                                                                VisualTreeHelper.GetDpi(MainWindow.visual).PixelsPerDip);
                dc.DrawText(formattedText, new Point(p.X + 5, p.Y - r - 15));
            }
        }
    }

    public class Prob
    {
        public int id;
        public double value;
    }
}
