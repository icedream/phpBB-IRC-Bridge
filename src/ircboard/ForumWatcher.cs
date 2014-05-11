/**
 * phpBB-to-IRC Bridge - Post forum updates to IRC.
 * Copyright (C) 2013 Icedream (Carl Kittelberger)
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 * 
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using IRCBoard.Properties;

namespace IRCBoard
{
    public class ForumWatcher
    {
        private CancellationTokenSource _cancellationTokenSource;
        private XDocument _currentAtomFeed;
        private Task _forumWatcherTask;

        public ForumWatcher()
        {
            LastUpdate = DateTime.Now;
            ForumID = 0;
            CheckInterval = TimeSpan.FromSeconds(30);
        }

        public Uri ForumBaseUrl { get; set; }
        public uint ForumID { get; set; }
        public TimeSpan CheckInterval { get; set; }

        public DateTime LastUpdate { get; private set; }

        public bool Alive
        {
            get { return _forumWatcherTask != null && _forumWatcherTask.Status == TaskStatus.Running; }
        }

        public event EventHandler<IncomingPostsEventArgs> PostsIncoming;

        protected void OnPostsIncoming(IncomingPostsEventArgs e)
        {
            if (PostsIncoming != null)
                PostsIncoming.Invoke(this, e);
        }

        public void Start()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _forumWatcherTask = Run();
            Console.WriteLine(Resources.MessageWatcherRunning);
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _forumWatcherTask.Wait();
        }

        private void RunSingle()
        {
            try
            {
                // Generate feed url
                var b = new UriBuilder(new Uri(ForumBaseUrl, "feed.php")) {Query = string.Format("f={0}", ForumID)};

                // Get the feed
                _currentAtomFeed = XDocument.Load(b.Uri.ToString());

                // Get posts since last update
                XElement[] posts = _currentAtomFeed.Root.Elements("{http://www.w3.org/2005/Atom}entry")
                    .Where(
                        pnode =>
                            DateTime.Parse(pnode.Element("{http://www.w3.org/2005/Atom}updated").Value, null,
                                DateTimeStyles.RoundtripKind) > LastUpdate)
                    .ToArray();

                // Check if any updated posts exist
                if (!posts.Any())
                    return;

                // Update last update timestamp
                LastUpdate = DateTime.Parse(
                    _currentAtomFeed.Root.Element("{http://www.w3.org/2005/Atom}updated").Value, null,
                    DateTimeStyles.RoundtripKind);

                // Trigger event
                OnPostsIncoming(new IncomingPostsEventArgs(posts));
            }
            catch
            {
                {
                } // TODO: Do something about this "ignore every error" bullshit
            }
        }

        private async Task Run()
            // TODO: WHAT THE FUCK DID I THINK OF MAKING THIS AN ENDLESS LOOP IN AN ASYNC FUNCTION HERE
        {
            var sw = new Stopwatch();
            while (true)
            {
                _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                sw.Reset();
                sw.Start();
                await Task.Run(new Action(RunSingle));
                while (sw.Elapsed < CheckInterval)
                {
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(
                        TimeSpan.FromMilliseconds(Math.Max(100, CheckInterval.TotalMilliseconds - sw.ElapsedMilliseconds)));
                }
                sw.Stop();
            }
        }
    }

    public class IncomingPostsEventArgs : EventArgs
    {
        internal IncomingPostsEventArgs(IEnumerable<XElement> postNodes)
        {
            PostNodes = postNodes;
        }

        public IEnumerable<XElement> PostNodes { get; private set; }
    }
}