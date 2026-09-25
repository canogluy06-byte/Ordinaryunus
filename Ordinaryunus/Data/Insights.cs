// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
namespace Ordinaryunus.Data;

/// <summary>Derived numbers and the rule-based (not AI) Turkish analysis sentences for the Durum page.</summary>
public static class Insights
{
    public static int StreakFrom(ISet<DateOnly> days, DateOnly today)
    {
        DateOnly d = days.Contains(today) ? today : today.AddDays(-1);
        int n = 0;
        while (days.Contains(d)) { n++; d = d.AddDays(-1); }
        return n;
    }

    public static DateOnly WeekStart(DateOnly today) => today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); // Monday

    public static (int claude, int codex) RequestsSince(VaultSnapshot s, DateOnly from)
    {
        int cl = 0, cx = 0;
        foreach (var e in s.Ledger)
        {
            if (e.Auto || DateOnly.FromDateTime(e.Local) < from) continue;
            if (e.Arac == "codex") cx++; else cl++;
        }
        return (cl, cx);
    }

    public static int CommitsSince(VaultSnapshot s, DateOnly from) =>
        s.Git.PerDay14.Where(kv => kv.Key >= from).Sum(kv => kv.Value);

    /// <summary>Per-day request counts for the last <paramref name="days"/> days (oldest first).</summary>
    public static List<(DateOnly day, int claude, int codex)> RequestsPerDay(VaultSnapshot s, int days)
    {
        var list = new List<(DateOnly, int, int)>();
        for (int i = days - 1; i >= 0; i--)
        {
            var d = s.Today.AddDays(-i);
            int cl = 0, cx = 0;
            foreach (var e in s.Ledger)
                if (!e.Auto && DateOnly.FromDateTime(e.Local) == d) { if (e.Arac == "codex") cx++; else cl++; }
            list.Add((d, cl, cx));
        }
        return list;
    }

    public static List<(DateOnly day, int commits)> CommitsPerDay(VaultSnapshot s, int days)
    {
        var list = new List<(DateOnly, int)>();
        for (int i = days - 1; i >= 0; i--)
        {
            var d = s.Today.AddDays(-i);
            list.Add((d, s.Git.PerDay14.GetValueOrDefault(d)));
        }
        return list;
    }

    public static List<string> Sentences(VaultSnapshot s)
    {
        var list = new List<string>();
        var today = s.Today;

        if (s.Focus is { } f && f.TotalCount > 0)
            list.Add($"Odak projende ({f.Name}) {f.TotalCount} maddeden {Poss(f.DoneCount)} bitti.");

        if (s.Todos.Items.Count > 0)
        {
            int open = s.Todos.Items.Count - s.Todos.DoneCount;
            list.Add(open == 0
                ? "Yapılacaklar listendeki her şey bitti. Harika; yeni işi Claude'la birlikte seçin."
                : $"Yapılacaklar listende {s.Todos.Items.Count} iş var; {Poss(s.Todos.DoneCount)} bitti, {open} tane kaldı.");
        }

        var (cl7, cx7) = RequestsSince(s, today.AddDays(-6));
        if (cl7 + cx7 > 0) list.Add($"Son 7 günde Codex'e {cx7}, Claude'a {cl7} istek verdin.");

        foreach (var p in s.Active.Where(p => p.KillDate is not null).OrderBy(p => p.KillDate))
        {
            int days = p.DaysToKill(today)!.Value;
            string cond = KillCondition(p.OlumKriteri);
            if (days < 0) list.Add($"{Gen(p.Name)} bırakma tarihi {-days} gün önce geçti; kararı yazma zamanı.");
            else list.Add($"{Gen(p.Name)} bırakma tarihine {days} gün var" + (cond.Length > 0 ? $": {cond}" : "."));
            if (list.Count >= 5) break;
        }

        int active = s.ActiveCount;
        if (active >= 3) list.Add($"{active}/3 aktif proje: sınır dolu, yeni proje açma.");
        else list.Add($"{active}/3 aktif proje: bir yer boş, ama doldurmak zorunda değilsin.");

        foreach (var p in s.Active)
        {
            if (p.SonGuncelleme is { } g && today.DayNumber - g.DayNumber >= 7)
                list.Add($"{p.Name} {today.DayNumber - g.DayNumber} gündür güncellenmedi.");
        }

        int commits7 = CommitsSince(s, today.AddDays(-6));
        if (s.Git.Available)
        {
            var best = s.Git.PerDay14.Where(kv => kv.Key >= today.AddDays(-6)).OrderByDescending(kv => kv.Value).FirstOrDefault();
            list.Add(commits7 == 0
                ? "Son 7 günde kasada kayıt (commit) yok."
                : $"Son 7 günde kasada {commits7} değişiklik kaydedildi; en hareketli gün {best.Key.ToString("d MMMM", Md.Tr)}.");
        }

        int approvals = s.ApprovalCount;
        if (approvals > 0) list.Add($"Onayını bekleyen {approvals} şey var; Masam'ın en üstünde.");

        return list.Take(8).ToList();
    }

    /// <summary>"Eğer 2026-10-31'e kadar 10 yazı yayınlanmazsa → tempo düşürülür. …" → "10 yazı yayınlanmazsa → tempo düşürülür".</summary>
    public static string KillCondition(string olum)
    {
        if (string.IsNullOrWhiteSpace(olum)) return "";
        string s = olum;
        int dot = s.IndexOf(". ", StringComparison.Ordinal);
        if (dot > 0) s = s[..dot];
        int kadar = s.IndexOf("kadar ", StringComparison.Ordinal);
        if (kadar >= 0) s = s[(kadar + 6)..];
        s = s.Trim().TrimEnd('.');
        int paren = s.IndexOf(" (", StringComparison.Ordinal);
        int close = paren > 0 ? s.IndexOf(')', paren) : -1;
        if (close > paren && paren > 0) s = s[..paren] + s[(close + 1)..];
        int arrow = s.IndexOf('→');
        int colon = arrow > 0 ? s.IndexOf(": ", arrow, StringComparison.Ordinal) : -1;
        if (colon > 0) s = s[..colon];
        s = Md.Clip(s, 110);
        return s.Length == 0 ? "" : char.ToLower(s[0], Md.Tr) + s[1..] + (s.EndsWith('…') ? "" : ".");
    }

    // ---------- Turkish suffix helpers ----------

    /// <summary>Number + 3rd person possessive/accusative suffix: 1'i, 2'si, 3'ü, 6'sı, 9'u, 10'u.</summary>
    public static string Poss(int n) => n + "'" + PossSuffix(n);

    static string PossSuffix(int n)
    {
        n = Math.Abs(n);
        if (n == 0) return "ı";
        int last = n % 10;
        if (last != 0)
            return last switch { 1 => "i", 2 => "si", 3 => "ü", 4 => "ü", 5 => "i", 6 => "sı", 7 => "si", 8 => "i", _ => "u" };
        int tens = n % 100 / 10;
        if (tens != 0)
            return tens switch { 1 => "u", 2 => "si", 3 => "u", 4 => "ı", 5 => "si", 6 => "ı", 7 => "i", 8 => "i", _ => "ı" };
        if (n % 1000 == 0) return "i"; // bin
        return "ü";                     // yüz
    }

    /// <summary>Özel ad için kesme işaretli tamlayan eki: "Defteri'nin", "Durumu'nun", "Blog'un", "Ordinaryunus'un".</summary>
    public static string Gen(string name)
    {
        string w = name.TrimEnd();
        if (w.Length == 0) return w;
        char lastVowel = 'e';
        for (int i = w.Length - 1; i >= 0; i--)
        {
            char c = char.ToLower(w[i], Md.Tr);
            if ("aeıioöuü".Contains(c)) { lastVowel = c; break; }
        }
        bool endsVowel = "aeıioöuü".Contains(char.ToLower(w[^1], Md.Tr));
        string v = lastVowel switch { 'a' or 'ı' => "ı", 'e' or 'i' => "i", 'o' or 'u' => "u", _ => "ü" };
        return w + "'" + (endsVowel ? "n" : "") + v + "n";
    }

    /// <summary>Dative suffix for a number as spoken: 1'e, 2'ye, 6'ya, 9'a, 10'a, 20'ye.</summary>
    static string DatSuffix(int n)
    {
        n = Math.Abs(n);
        if (n == 0) return "a"; // sıfır
        int last = n % 10;
        if (last != 0)
            return last switch { 1 => "e", 2 => "ye", 3 => "e", 4 => "e", 5 => "e", 6 => "ya", 7 => "ye", 8 => "e", _ => "a" };
        int tens = n % 100 / 10;
        return tens switch { 1 => "a", 2 => "ye", 3 => "a", 4 => "a", 5 => "ye", 6 => "a", 7 => "e", 8 => "e", 9 => "a", _ => "e" };
    }

    /// <summary>"22:00'ye", "10:30'a", "09:05'e" (suffix follows the last spoken number).</summary>
    public static string TimeDat(TimeOnly t)
    {
        int spoken = t.Minute != 0 ? t.Minute : t.Hour;
        return t.ToString("HH:mm") + "'" + DatSuffix(spoken);
    }

    public static string DaysText(int days) => days switch
    {
        0 => "bugün",
        1 => "yarın",
        < 0 => $"{-days} gün geçti",
        _ => $"{days} gün kaldı",
    };
}
