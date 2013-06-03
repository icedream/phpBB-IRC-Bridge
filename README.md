phpBB-to-IRC bridge
===================

This bot allows your phpBB forum updates to be copied over to IRC.

Requirements
============

- A working C# compiler (to compile the source code)
- Some kind of .NET library (latest Mono/.NET Framework 4)
- A forum with enabled RSS feeds

Configuration
=============

```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>

  <!-- The language used by the bot -->
  <interface>
    <language>en-US</language>
  </interface>
  
  <!-- Your target server details -->
  <server>
    <host>irc.rizon.net</host>
    <port>6667</port>
    <secure>false</secure>
    <autoreconnect>true</autoreconnect>
    <password></password>
  </server>
  
  <!-- Your bot's identification details -->
  <bot>
    <nickname>FourDeltaOne-Forum</nickname>
    <username>4d1-forum</username>
    <realname>#alteriwnet-german Forum Bot</realname>
    <invisible>false</invisible>
  </bot>
  
  <!-- NickServer identification details -->
  <nickserv>
    <authentication></authentication>
  </nickserv>
  
  <!-- Forum details -->
  <forum>
    <baseurl>http://fourdeltaone.net/forum/</baseurl>
    <subforum-id>20</subforum-id>
    <checkinterval>00:00:30</checkinterval><!-- HH:MM:SS -->
  </forum>
  
  <!-- The target channels -->
  <channels>
    <channel>#alterIWnet-German</channel>
    <channel>#fourdeltaone-german</channel>
  </channels>
  
</configuration>
```

Should be self-explanatory so far.

License
=======

The license under which source code and binaries are released is the _GNU Affero General Public License (AGPL) Version 3_.
More infos in the LICENSE.txt file.