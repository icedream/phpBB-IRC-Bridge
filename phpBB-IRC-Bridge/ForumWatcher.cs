using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace ForumBot_German4D1
{
    public class ForumWatcher
    {
        public Uri ForumBaseUrl { get; set; }
        public uint ForumID { get; set; }
        public TimeSpan CheckInterval { get; set; }

        public event EventHandler<IncomingPostsEventArgs> PostsIncoming;

        public DateTime LastUpdate { get; private set; }

        protected void OnPostsIncoming(IncomingPostsEventArgs e)
        {
            if (PostsIncoming != null)
                PostsIncoming.Invoke(this, e);
        }

        private XDocument _currentAtomFeed;
        private Task ForumWatcherTask;
        private CancellationTokenSource CancellationTokenSource;

        public bool Alive { get { return ForumWatcherTask != null && ForumWatcherTask.Status == TaskStatus.Running; } }

        public ForumWatcher()
        {
            LastUpdate = DateTime.Now;
            ForumID = 0;
            CheckInterval = TimeSpan.FromSeconds(30);
        }

        public void Start()
        {
            CancellationTokenSource = new System.Threading.CancellationTokenSource();
            ForumWatcherTask = Run();
            Console.WriteLine("Watcher running.");
        }

        public void Stop()
        {
            CancellationTokenSource.Cancel();
            ForumWatcherTask.Wait();
        }

        private void RunSingle()
        {
            // Generate feed url
            UriBuilder b = new UriBuilder(new Uri(ForumBaseUrl, "feed.php"));
            b.Query = string.Format("f={0}", ForumID);

            // Get the feed
            _currentAtomFeed = XDocument.Load(b.Uri.ToString());

            // Get posts since last update
            var posts = _currentAtomFeed.Root.Elements("{http://www.w3.org/2005/Atom}entry")
                    .Where(pnode => DateTime.Parse(pnode.Element("{http://www.w3.org/2005/Atom}updated").Value, null, DateTimeStyles.RoundtripKind) > LastUpdate)
                    .ToArray();

            // Check if any updated posts exist
            if (posts.Any())
            {
                // Update last update timestamp
                LastUpdate = DateTime.Parse(_currentAtomFeed.Root.Element("{http://www.w3.org/2005/Atom}updated").Value, null, DateTimeStyles.RoundtripKind);

                // Trigger event
                OnPostsIncoming(new IncomingPostsEventArgs(posts));
            }
        }

        private async Task Run()
        {
            Stopwatch sw = new Stopwatch();
            while (true)
            {
                if (CancellationTokenSource.Token.IsCancellationRequested)
                    break;

                sw.Restart();
                await Task.Run(new Action(this.RunSingle));
                while (!CancellationTokenSource.Token.IsCancellationRequested && sw.Elapsed < CheckInterval)
                    Thread.Sleep(TimeSpan.FromMilliseconds(Math.Max(0, (double)(CheckInterval.TotalMilliseconds - sw.ElapsedMilliseconds))));
            }
        }
    }

    public class IncomingPostsEventArgs : EventArgs
    {
        public IEnumerable<XElement> PostNodes { get; private set; }

        internal IncomingPostsEventArgs(IEnumerable<XElement> postNodes)
        {
            PostNodes = postNodes;
        }
    }
}
