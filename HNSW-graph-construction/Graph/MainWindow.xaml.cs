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

        HNSWGraph hnsw;
        int nodes_count;
        Point? mouse;

        public MainWindow()
        {
            InitializeComponent();

            visual = new DrawingVisual();
            width = (int)g.Width;
            height = (int)g.Height;

            Init();

            timer.Tick += Timer_Tick;
            timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
        }

        private void Init()
        {
            timer.Stop();

            hnsw = new HNSWGraph(rnd, width, height);
            hnsw.MessageNotify += msg =>
            {
                rtbConsole.AppendText(msg);
                rtbConsole.ScrollToEnd();
            };
            nodes_count = 60;

            rtbConsole.Document.Blocks.Clear();
            rtbConsole.AppendText("=== Hierarchical Navigable Small World (HNSW) Graph ===\n\r");
            rtbConsole.AppendText($"Nodes: {nodes_count}, Layers: {hnsw.LayerCount}\n");
            rtbConsole.AppendText("Controls:\n");
            rtbConsole.AppendText("• 'Build Step by Step' - Build graph gradually\n");
            rtbConsole.AppendText("• 'Build Instant' - Build graph immediately\n");
            rtbConsole.AppendText("• Click on canvas to set query point (red)\n");
            rtbConsole.AppendText("• Watch search process with colored nodes\n");
            rtbConsole.AppendText("------------------------------------------------\n");

            Drawing();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!hnsw.IsBuildComplete)
            {
                hnsw.BuildStepByStep(nodes_count);
                Drawing();
                
                // Update status
                if (hnsw.IsBuildComplete)
                {
                    btnBuild.Content = "Build Complete";
                    btnBuild.IsEnabled = false;
                }
                else
                {
                    btnBuild.Content = $"Building... ({hnsw.Nodes.Count}/{nodes_count})";
                }
            }
            else
            {
                timer.Stop();
                btnBuild.Content = "Build Complete";
                btnBuild.IsEnabled = false;
            }
        }

        private void btnBuild_Click(object sender, RoutedEventArgs e)
        {
            if (!timer.IsEnabled)
            {
                // Start building
                hnsw.Reset();
                timer.Start();
                btnBuild.Content = "Stop Building";
                btnBuildInstant.IsEnabled = false;
                
                rtbConsole.AppendText("\nStarting step-by-step construction...\n");
            }
            else
            {
                // Stop building
                timer.Stop();
                btnBuild.Content = "Build Step by Step";
                btnBuildInstant.IsEnabled = true;
                
                rtbConsole.AppendText("\nConstruction stopped.\n");
            }
        }

        private void Drawing()
        {
            g.RemoveVisual(visual);
            using (dc = visual.RenderOpen())
            {
                // Draw background
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
                
                hnsw.Draw(dc);
                dc.Close();
                g.AddVisual(visual);
            }
        }

        private void g_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!hnsw.IsBuildComplete || hnsw.Nodes.Count == 0)
            {
                rtbConsole.AppendText("\nPlease build the graph first!\n");
                return;
            }

            mouse = e.GetPosition(g);
            hnsw.SetQueryPoint((Point)mouse);

            rtbConsole.AppendText($"\nQuery point set at: X={((Point)mouse).X:F0}, Y={((Point)mouse).Y:F0}\n");
            rtbConsole.AppendText("Finding shortest path from EP...\n");

            hnsw.SearchShortestPath();
            Drawing();
        }

        private void btnBuildInstant_Click(object sender, RoutedEventArgs e)
        {
            rtbConsole.AppendText("\nBuilding graph instantly...\n");
            
            hnsw.BuildComplete(nodes_count);
            btnBuild.Content = "Build Step by Step";
            btnBuild.IsEnabled = true;
            btnBuildInstant.IsEnabled = true;
            
            Drawing();
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            timer.Stop();
            Init();
            btnBuild.Content = "Build Step by Step";
            btnBuild.IsEnabled = true;
            btnBuildInstant.IsEnabled = true;
            
            rtbConsole.AppendText("\nReset complete. Ready for new graph.\n");
        }

        private void btnClearConsole_Click(object sender, RoutedEventArgs e)
        {
            rtbConsole.Document.Blocks.Clear();
            rtbConsole.AppendText("Console cleared.\n");
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            timer.Stop();
        }
    }
}