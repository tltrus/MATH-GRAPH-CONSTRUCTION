using System;
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

        Graph NSW;
        int nodes_count;

        public MainWindow()
        {
            InitializeComponent();

            visual = new DrawingVisual();

            width = (int)g.Width;
            height = (int)g.Height;

            Init();

            timer.Tick += Tick;
            timer.Interval = new TimeSpan(0,0,0,0, 600);
        }

        private void Init()
        {
            timer.Stop();

            NSW = new Graph(rnd, width, height);
            NSW.MessageNotify += msg =>
            {
                rtbConsole.AppendText(msg);
            };
            nodes_count = 50;

            rtbConsole.Document.Blocks.Clear();
            rtbConsole.AppendText("Navigable Small World (NSW)." 
                                    + "\nNodes number is " + nodes_count
                                    + ".\n--> Create NSW graph."
                                    + "\n-------------");
            Drawing();
        }

        private void Tick(object sender , EventArgs e)
        {
            NSW.ConstructionDynamic(nodes_count);
            Drawing();
        }

        private void btnDynamic_Click(object sender, RoutedEventArgs e)
        {
            Init();
            if (!timer.IsEnabled)
                timer.Start();
            else
                timer.Stop();
        }

        private void Drawing()
        {
            g.RemoveVisual(visual);
            using (dc = visual.RenderOpen())
            {
                NSW.Draw(dc);

                dc.Close();
                g.AddVisual(visual);
            }
        }

        private void g_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }

        private void btnStatic_Click(object sender, RoutedEventArgs e)
        {
            Init();
            NSW.ConstructionStatic(nodes_count);
            Drawing();
        }

    }
}
