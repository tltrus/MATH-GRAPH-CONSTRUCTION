using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace MathGraph
{
    public partial class MainWindow : Window
    {
        Random rnd = new Random();
        DispatcherTimer timer = new DispatcherTimer();
        int width, height;
        public static DrawingVisual visual;
        DrawingContext dc;
        Point mouse;

        Graph Graph;

        public MainWindow()
        {
            InitializeComponent();

            visual = new DrawingVisual();

            width = (int)g.Width;
            height = (int)g.Height;

            Init();

            timer.Tick += Tick;
            timer.Interval = new TimeSpan(0,0,0,0,100);
        }

        private void Init()
        {
            Graph = new Graph(rnd, width, height);
            int m = rnd.Next(1, 10);
            Graph.Initialization(nodes_num: 20, m_param: m);
            Graph.MessageNotify += msg =>
            {
                rtbConsole.AppendText(msg);
            };

            rtbConsole.Document.Blocks.Clear();
            rtbConsole.AppendText("Graph construction / Barabási–Albert network model\nm parameter is " + m + "\nClick any mouse button to add new point\n");

            Drawing();
        }

        private void Tick(object sender , EventArgs e) => Drawing();

        private void Drawing()
        {
            g.RemoveVisual(visual);
            using (dc = visual.RenderOpen())
            {
                Graph.Draw(dc);

                dc.Close();
                g.AddVisual(visual);
            }
        }

        private void g_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            mouse = e.GetPosition(g);

            Graph.AddPoint(mouse);
            Drawing();
        }

        private void btnClear_Click(object sender, RoutedEventArgs e) => Init();
    }
}
