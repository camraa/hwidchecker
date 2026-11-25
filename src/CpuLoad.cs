// Charge du processeur, mesuree par quatre moyens independants.
// Sur certains Windows 11, GetSystemTimes surestime l'inactivite et donne 0 % en permanence.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace HwidChecker
{
    public static class CpuLoad
    {
        public static readonly string[] Names = { "PDH utility", "PDH time", "idle information", "GetSystemTimes" };

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        static extern uint PdhOpenQuery(string source, IntPtr user, out IntPtr query);
        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        static extern uint PdhAddEnglishCounter(IntPtr query, string path, IntPtr user, out IntPtr counter);
        [DllImport("pdh.dll")]
        static extern uint PdhCollectQueryData(IntPtr query);
        [StructLayout(LayoutKind.Explicit, Size = 16)]
        struct PdhValue { [FieldOffset(0)] public uint Status; [FieldOffset(8)] public double Value; }
        [DllImport("pdh.dll")]
        static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out PdhValue value);

        [DllImport("ntdll.dll")]
        static extern int NtQuerySystemInformation(int cls, byte[] buf, int len, out int ret);
        [DllImport("kernel32.dll")]
        static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

        static IntPtr query, utility, time;
        static bool opened;
        static long lastIdleInfo = -1, lastClock, lastIdle, lastTotal;
        static readonly Queue<double>[] recent = Enumerable.Range(0, 4).Select(i => new Queue<double>()).ToArray();

        /// <summary>Valeurs brutes des quatre sources (NaN si indisponible), pour le diagnostic.</summary>
        public static readonly double[] Raw = new double[4];

        public static double Read()
        {
            ReadPdh();
            Raw[2] = IdleInformation();
            Raw[3] = SystemTimes();
            for (int i = 0; i < 4; i++)
            {
                if (double.IsNaN(Raw[i])) continue;
                Raw[i] = Math.Max(0, Math.Min(100, Raw[i]));
                recent[i].Enqueue(Raw[i]);
                if (recent[i].Count > 10) recent[i].Dequeue();
            }
            // Premiere source, par ordre de preference, qui n'est pas bloquee a zero.
            for (int i = 0; i < 4; i++)
                if (!double.IsNaN(Raw[i]) && recent[i].Max() >= 0.5) return Raw[i];
            var valid = Raw.Where(v => !double.IsNaN(v)).ToList();
            return valid.Count == 0 ? double.NaN : valid.Max();
        }

        static void ReadPdh()
        {
            Raw[0] = Raw[1] = double.NaN;
            try
            {
                if (!opened)
                {
                    opened = true;
                    if (PdhOpenQuery(null, IntPtr.Zero, out query) != 0) query = IntPtr.Zero;
                    else
                    {
                        // Meme compteur que le Gestionnaire des taches ; noms anglais, valables dans toutes les langues.
                        if (PdhAddEnglishCounter(query, @"\Processor Information(_Total)\% Processor Utility", IntPtr.Zero, out utility) != 0) utility = IntPtr.Zero;
                        if (PdhAddEnglishCounter(query, @"\Processor Information(_Total)\% Processor Time", IntPtr.Zero, out time) != 0) time = IntPtr.Zero;
                    }
                }
                if (query == IntPtr.Zero || PdhCollectQueryData(query) != 0) return;
                Raw[0] = Value(utility);
                Raw[1] = Value(time);
            }
            catch { query = IntPtr.Zero; }
        }

        static double Value(IntPtr counter)
        {
            uint type;
            PdhValue v;
            return counter != IntPtr.Zero && PdhGetFormattedCounterValue(counter, 0x200 | 0x8000, out type, out v) == 0 && v.Status <= 1
                ? v.Value : double.NaN; // format double, sans plafond a 100
        }

        // SystemProcessorIdleInformation : temps d'inactivite reel de chaque processeur logique.
        static double IdleInformation()
        {
            const int size = 48;
            int n, cpus = Environment.ProcessorCount;
            var b = new byte[size * Math.Max(cpus, 64)];
            long clock = Stopwatch.GetTimestamp();
            if (NtQuerySystemInformation(42, b, b.Length, out n) != 0 || n < size) return double.NaN;
            cpus = n / size;
            long idle = 0;
            for (int i = 0; i < cpus; i++) idle += BitConverter.ToInt64(b, i * size);
            double load = double.NaN, elapsed = (clock - lastClock) * 1e7 / Stopwatch.Frequency * cpus; // en 100 ns
            if (lastIdleInfo >= 0 && elapsed > 0) load = 100 * (1 - (idle - lastIdleInfo) / elapsed);
            lastIdleInfo = idle;
            lastClock = clock;
            return load;
        }

        static double SystemTimes()
        {
            long idle, kernel, user;
            if (!GetSystemTimes(out idle, out kernel, out user)) return double.NaN;
            long total = kernel + user; // le temps noyau inclut l'inactivite
            double load = lastTotal > 0 && total > lastTotal ? 100.0 * (1 - (double)(idle - lastIdle) / (total - lastTotal)) : double.NaN;
            lastIdle = idle;
            lastTotal = total;
            return load;
        }
    }
}
