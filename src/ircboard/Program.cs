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
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Floe.Net;
using IRCBoard.Properties;

namespace IRCBoard
{
    internal class Program
    {
        private XmlDocument _config;
        private IrcSession _irc;
        private ForumWatcher _watcher;

        private static void Main(string[] args)
        {
            var doc = new XmlDocument();
            if (!args.Any())
                args = new[] {"Configuration.xml"};
            doc.Load(args.First());
            var p = new Program();
            p.Run(doc);
            Thread.Sleep(-1);
        }

        public void Run(XmlDocument configuration)
        {
            // Save configuration to instance
            _config = configuration;

            // Set localization
            if (
                !configuration.SelectSingleNode("//configuration/interface/language")
                    .InnerText.Equals("auto", StringComparison.OrdinalIgnoreCase))
                Thread.CurrentThread.CurrentUICulture =
                    CultureInfo.GetCultureInfo(
                        configuration.SelectSingleNode("//configuration/interface/language").InnerText);

            // Create IRC session (uninitialized)
            _irc = new IrcSession();

            // On connection errors
            _irc.ConnectionError +=
                (s, e) => { Console.WriteLine(Resources.MessageErrorOccurred, e.Exception.ToString()); };

            // Fix for kicks
            _irc.SelfKicked += (s, e) =>
            {
                Thread.Sleep(TimeSpan.FromSeconds(5));
                _irc.Join(e.Channel.Name);
            };

            // Triggered when someone joins a channel
            _irc.Joined +=
                (s, e) => { Console.WriteLine(Resources.MessageIRCUserJoinedChannel, e.Channel.Name, e.Who.Nickname); };

            // Triggered when someone leaves a channel
            _irc.Parted +=
                (s, e) => { Console.WriteLine(Resources.MessageIRCUserLeftChanne, e.Channel.Name, e.Who.Nickname); };

            // Triggered when someone changes his nickname
            _irc.NickChanged +=
                (s, e) => { Console.WriteLine(Resources.MessageIRCUserChangedNickname, e.NewNickname, e.OldNickname); };

            // Triggered when we join a channel
            _irc.SelfJoined += (s, e) => { Console.WriteLine(Resources.MessageBotJoinedChannel, e.Channel.Name); };

            // Triggered when we leave a channel
            _irc.SelfParted += (s, e) => { Console.WriteLine(Resources.MessageBotLeftChannel, e.Channel.Name); };

            // Triggered when we change our nickname
            _irc.SelfNickChanged += (s, e) => { Console.WriteLine(Resources.MessageBotChangedNickname, e.NewNickname); };

            // Triggered on connection state change
            _irc.StateChanged += (s, e) =>
            {
                switch (_irc.State)
                {
                    case IrcSessionState.Connected:
                        Console.WriteLine(Resources.MessageIRCConnectionSuccess);
                        if (!_watcher.Alive)
                            _watcher.Start();
                        break;
                    case IrcSessionState.Disconnected:
                        Console.WriteLine(Resources.MessageIRCDisconnected);
                        break;
                    case IrcSessionState.Connecting:
                        Console.WriteLine(Resources.MessageIRCConnecting);
                        break;
                    default:
                        Console.WriteLine(Resources.MessageIRCSessionStateChanged, _irc.State.ToString());
                        break;
                }
            };

            // Join on invite
            _irc.Invited += (s, e) =>
            {
                // Check if channel is in autojoin list
                if (_config.SelectNodes("//configuration/channels/channel")
                    .Cast<XmlNode>().Any(node => node.InnerText.Equals(e.Channel, StringComparison.OrdinalIgnoreCase)))
                {
                    // Join the channel
                    _irc.Join(e.Channel);
                }
            };

            // Identify when NickServ requests it
            _irc.PrivateMessaged += (s, e) =>
            {
                if (e.From.Nickname == "NickServ" && e.Text.Contains("This nickname is registered"))
                    _irc.PrivateMessage(new IrcTarget(e.From),
                        string.Format("IDENTIFY {0}",
                            _config.SelectSingleNode("//configuration/nickserv/authentication").InnerText));
                _irc.PrivateMessage(new IrcTarget("HostServ"), "ON");
            };

            // Reply to CTCP Version
            _irc.CtcpCommandReceived += (s, e) =>
            {
                Console.WriteLine("Received CTCP {0} from {1}", e.Command.Command, e.From.Nickname);
                switch (e.Command.Command.ToUpper())
                {
                    case "VERSION":
                        _irc.SendCtcp(new IrcTarget(e.From), new CtcpCommand("VERSION", "phpBB IRC bot"), true);
                        break;
                }
            };

            /*
#if DEBUG
            // Print out raw messages being received
            _irc.RawMessageReceived += (s, e) =>
            {
                Debug.WriteLine("<= {0}", e.Message.ToString(), null);
            };

            // Print out raw messages being sent
            _irc.RawMessageSent += (s, e) =>
            {
                Debug.WriteLine("=> {0}", e.Message.ToString(), null);
            };
#endif
             */

            // Initialize and connect IRC session
            Console.WriteLine(Resources.MessageDebugPrintHost,
                configuration.SelectSingleNode("//configuration/server/host").InnerText);
            Console.WriteLine(Resources.MessageDebugPrintPort,
                configuration.SelectSingleNode("//configuration/server/port").InnerText);
            Console.WriteLine(Resources.MessageDebugPrintSSL,
                Convert.ToBoolean(configuration.SelectSingleNode("//configuration/server/secure").InnerText));

            _irc.AutoReconnect = false;
            _irc.Open(
                configuration.SelectSingleNode("//configuration/server/host").InnerText,
                Convert.ToUInt16(configuration.SelectSingleNode("//configuration/server/port").InnerText),
                Convert.ToBoolean(configuration.SelectSingleNode("//configuration/server/secure").InnerText),
                configuration.SelectSingleNode("//configuration/bot/nickname").InnerText,
                configuration.SelectSingleNode("//configuration/bot/username").InnerText,
                configuration.SelectSingleNode("//configuration/bot/realname").InnerText,
                Convert.ToBoolean(configuration.SelectSingleNode("//configuration/server/autoreconnect").InnerText),
                configuration.SelectSingleNode("//configuration/server/password").InnerText,
                Convert.ToBoolean(configuration.SelectSingleNode("//configuration/bot/invisible").InnerText),
                false
                );

            // Fix for existing nicks
            _irc.AddHandler(new IrcCodeHandler(e =>
            {
                _irc.Nick(_irc.Nickname + "`");
                return false; // Do not remove handler
            }, IrcCode.ERR_NICKCOLLISION, IrcCode.ERR_NICKNAMEINUSE));

            // Fix for invitation-lockout
            _irc.AddHandler(new IrcCodeHandler(e =>
            {
                _irc.PrivateMessage(new IrcTarget("ChanServ"), "INVITE " + e.Text.Split(' ').First());
                return false; // Do not remove handler
            }, IrcCode.ERR_INVITEONLYCHAN));

            // Fix for bans
            _irc.AddHandler(new IrcCodeHandler(e =>
            {
                _irc.PrivateMessage(new IrcTarget("ChanServ"), "UNBAN " + e.Text.Split(' ').First());
                return false; // Do not remove handler
            }, IrcCode.ERR_BANNEDFROMCHAN));

            // Automatically join configured channels
            _irc.AddHandler(new IrcCodeHandler(e =>
            {
                foreach (string channel in _config.SelectNodes("//configuration/channels/channel")
                    .Cast<XmlNode>().Select(n => n.InnerText))
                    _irc.Join(channel);
                return true; // remove handler afterwards
            }, IrcCode.RPL_ENDOFMOTD));

            // Initialize and run forum watcher
            _watcher = new ForumWatcher
            {
                ForumBaseUrl = new Uri(configuration.SelectSingleNode("//configuration/forum/baseurl").InnerText),
                ForumID =
                    Convert.ToUInt32(configuration.SelectSingleNode("//configuration/forum/subforum-id").InnerText),
                CheckInterval =
                    TimeSpan.Parse(configuration.SelectSingleNode("//configuration/forum/checkinterval").InnerText)
            };
            _watcher.PostsIncoming += (s, e) =>
            {
                try
                {
                    foreach (XElement post in e.PostNodes)
                    {
                        foreach (
                            string channel in
                                _config.SelectNodes("//configuration/channels/channel")
                                    .Cast<XmlNode>()
                                    .Select(n => n.InnerText))
                        {
                            var target = new IrcTarget(channel);

                            string published = post.Element("{http://www.w3.org/2005/Atom}published").Value;
                            string updated = post.Element("{http://www.w3.org/2005/Atom}updated").Value;
                            string title = post.Element("{http://www.w3.org/2005/Atom}title").Value;
                            string author = post.Element("{http://www.w3.org/2005/Atom}author").Value;
                            string href = post.Element("{http://www.w3.org/2005/Atom}link").Attribute("href").Value;

                            string message =
                                string.Format(
                                    "\x02{0}\x02: {1} {2} {3} - {4}",

                                    // "New post"
                                    Resources.NewPost,

                                    // Thread title
                                    title,

                                    // "by"
                                    Resources.By,

                                    // Author
                                    author,

                                    // URL to post
                                    href
                                    );
                            _irc.PrivateMessage(
                                target,
                                message
                                );
                        }
                    }
                }
                catch (Exception err)
                {
                    Console.WriteLine(err);
                }
            };
        }
    }
}