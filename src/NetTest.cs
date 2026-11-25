// Test reseau : ping (ICMP), pertes, gigue et temps de reponse DNS.
// Pas de mesure TCP : les proxys d'entreprise ou d'ecole repondent a la place du serveur.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HwidChecker
{
    public class Probe
    {
        public string Group, Name, Host;
        public bool Dns;
        public List<double> Times = new List<double>();
        public int Sent;

        public int Loss { get { return Sent == 0 ? 0 : (int)Math.Round(100.0 * (Sent - Times.Count) / Sent); } }
        public double Average { get { return Times.Count == 0 ? double.NaN : Times.Average(); } }
        public double Jitter
        {
            get { return Times.Count < 2 ? 0 : Enumerable.Range(1, Times.Count - 1).Average(i => Math.Abs(Times[i] - Times[i - 1])); }
        }

        public string State
        {
            get
            {
                if (Times.Count == 0) return null; // beaucoup de serveurs ignorent le ping : ce n'est pas un defaut
                if (Dns) return Average > 150 ? "off" : Average > 60 || Loss > 0 ? "warn" : "on";
                return Loss >= 5 || Average > 150 ? "off" : Loss > 0 || Average > 80 || Jitter > 15 ? "warn" : "on";
            }
        }
    }

    public static class NetTest
    {
        public static List<Probe> Targets()
        {
            var l = new List<Probe>();
            var up = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback).ToList();
            var gw = up.SelectMany(n => n.GetIPProperties().GatewayAddresses).Select(g => g.Address)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any));
            if (gw != null) l.Add(new Probe { Group = "Connexion", Name = "Passerelle", Host = gw.ToString() });
            l.Add(new Probe { Group = "Connexion", Name = "Cloudflare", Host = "1.1.1.1" });
            l.Add(new Probe { Group = "Connexion", Name = "Google", Host = "8.8.8.8" });
            l.Add(new Probe { Group = "Serveurs de jeux", Name = "Fortnite Europe", Host = "ping-eu.ds.on.epicgames.com" });
            l.Add(new Probe { Group = "Serveurs de jeux", Name = "Fortnite Moyen-Orient", Host = "ping-me.ds.on.epicgames.com" });
            l.Add(new Probe { Group = "Serveurs de jeux", Name = "Fortnite Amérique du Nord (est)", Host = "ping-nae.ds.on.epicgames.com" });
            l.Add(new Probe { Group = "Serveurs de jeux", Name = "Fortnite Asie", Host = "ping-asia.ds.on.epicgames.com" });
            l.Add(new Probe { Group = "Centres de données AWS", Name = "Paris", Host = "dynamodb.eu-west-3.amazonaws.com" });
            l.Add(new Probe { Group = "Centres de données AWS", Name = "Francfort", Host = "dynamodb.eu-central-1.amazonaws.com" });
            l.Add(new Probe { Group = "Centres de données AWS", Name = "Londres", Host = "dynamodb.eu-west-2.amazonaws.com" });
            l.Add(new Probe { Group = "Centres de données AWS", Name = "Virginie", Host = "dynamodb.us-east-1.amazonaws.com" });
            var dns = up.Where(n => n.GetIPProperties().GatewayAddresses.Count > 0).SelectMany(n => n.GetIPProperties().DnsAddresses)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            if (dns != null) l.Add(new Probe { Group = "Serveurs DNS", Name = "DNS de ce PC", Host = dns.ToString(), Dns = true });
            l.Add(new Probe { Group = "Serveurs DNS", Name = "Cloudflare", Host = "1.1.1.1", Dns = true });
            l.Add(new Probe { Group = "Serveurs DNS", Name = "Google", Host = "8.8.8.8", Dns = true });
            l.Add(new Probe { Group = "Serveurs DNS", Name = "Quad9", Host = "9.9.9.9", Dns = true });
            return l;
        }

        public static void Run(List<Probe> list, Action<int, int> progress)
        {
            int done = 0;
            Parallel.ForEach(list, new ParallelOptions { MaxDegreeOfParallelism = 6 }, p =>
            {
                try { if (p.Dns) DnsProbe(p); else PingProbe(p); } catch { /* sans reponse */ }
                progress(Interlocked.Increment(ref done), list.Count);
            });
        }

        static void PingProbe(Probe p)
        {
            IPAddress ip;
            if (!IPAddress.TryParse(p.Host, out ip))
                ip = System.Net.Dns.GetHostAddresses(p.Host).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            if (ip == null) return;
            using (var ping = new Ping())
                for (int i = 0; i < 10; i++)
                {
                    p.Sent++;
                    var r = ping.Send(ip, 1200);
                    if (r.Status == IPStatus.Success) p.Times.Add(r.RoundtripTime);
                    if (p.Sent == 3 && p.Times.Count == 0) return; // muet : inutile d'insister
                    Thread.Sleep(120);
                }
        }

        static void DnsProbe(Probe p)
        {
            foreach (var name in new[] { "epicgames.com", "github.com", "wikipedia.org", "steampowered.com", "microsoft.com" })
            {
                p.Sent++;
                using (var u = new UdpClient())
                {
                    u.Client.ReceiveTimeout = 1500;
                    u.Connect(p.Host, 53);
                    var q = Query(name);
                    var sw = Stopwatch.StartNew();
                    u.Send(q, q.Length);
                    try
                    {
                        IPEndPoint from = null;
                        var a = u.Receive(ref from);
                        if (a.Length > 12 && a[0] == q[0] && a[1] == q[1]) p.Times.Add(sw.Elapsed.TotalMilliseconds);
                    }
                    catch (SocketException) { if (p.Sent == 2 && p.Times.Count == 0) return; }
                }
            }
        }

        static readonly Random random = new Random();

        static byte[] Query(string host)
        {
            int id;
            lock (random) id = random.Next(1, 65535);
            var l = new List<byte> { (byte)(id >> 8), (byte)id, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0 };
            foreach (var part in host.Split('.'))
            {
                l.Add((byte)part.Length);
                l.AddRange(Encoding.ASCII.GetBytes(part));
            }
            l.AddRange(new byte[] { 0, 0, 1, 0, 1 }); // fin du nom, type A, classe IN
            return l.ToArray();
        }
    }
}
