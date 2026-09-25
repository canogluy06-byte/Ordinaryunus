// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
using System.Globalization;
using Ordinaryunus.Bridge;
using Ordinaryunus.Data;

namespace Ordinaryunus.Brain;

/// <summary>Rule-based problem detection (MIMARI §5.8). Every rule is independent and cheap; run on every reload.</summary>
public static class ProblemDetector
{
    public static List<ProblemDto> Run(VaultSnapshot snap, BrainIndex index, IReadOnlyList<JobDto> jobs,
        IReadOnlyList<LightDto> lights, IReadOnlyDictionary<string, List<KayitEntry>> kayitByProject, DateTime now)
    {
        var list = new List<ProblemDto>();
        var today = DateOnly.FromDateTime(now);

        // P01 — broken wikilinks (one problem per note that has any, skipping templates)
        foreach (var note in index.Notes.Values.OrderBy(n => n.RelPath, StringComparer.Ordinal))
        {
            if (note.Kind == "sablon") continue;
            if (!index.Broken.TryGetValue(note.RelPath, out var broken) || broken.Count == 0) continue;
            string names = string.Join(", ", broken.Take(5).Select(b => b.Target));
            list.Add(new ProblemDto($"P01:{note.RelPath}", "P01", "dikkat", "not", note.Project,
                $"«{note.Title}» notunda {broken.Count} kırık bağlantı var", "Kırık bağlantılar: " + names,
                "Bağlantıyı düzelt ya da hedef notu oluştur.", new NoteTarget(note.RelPath, null), null));
        }

        foreach (var p in snap.Projects)
        {
            bool active = p.Durum == "aktif";
            bool ended = p.Durum is "bitti" or "donduruldu";
            string cardPath = $"20 Projeler/{p.Name}/{p.Name}.md";

            // P02 — missing required fields
            if (!ended)
            {
                string sev = active ? "dikkat" : "bilgi";
                void Missing(string field, string val, string label)
                {
                    if (!string.IsNullOrWhiteSpace(val)) return;
                    list.Add(new ProblemDto($"P02:{p.Name}:{field}", "P02", sev, "proje", p.Name,
                        $"{p.Name}: {label} yazılmamış", "", "Proje kartına ekle.", new NoteTarget(cardPath, null), null));
                }
                Missing("bitti_tanimi", p.BittiTanimi, "bitti tanımı");
                Missing("sonraki_adim", p.SonrakiAdim, "sonraki adım");
                Missing("olum_kriteri", p.OlumKriteri, "ölüm kriteri");

                // P02b — no checkable items, or no Engel paragraph
                if (p.TotalCount == 0)
                    list.Add(new ProblemDto($"P02b:{p.Name}:bos", "P02b", "dikkat", "proje", p.Name,
                        $"{p.Name}: bitti tanımında işaretlenecek madde yok", "", "\"## Bitti Tanımı\" altına en az bir madde ekle.",
                        new NoteTarget(cardPath, null), null));
                if (string.IsNullOrWhiteSpace(p.Engel))
                    list.Add(new ProblemDto($"P02b:{p.Name}:engel", "P02b", "bilgi", "proje", p.Name,
                        $"{p.Name}: \"## Engel\" bölümü yok", "", "En zor parçayı bir cümleyle yaz.",
                        new NoteTarget(cardPath, null), null));
            }

            // P03 — kill date
            if (!ended)
            {
                int? days = p.DaysToKill(today);
                if (days is { } d)
                {
                    if (d < 0)
                        list.Add(new ProblemDto($"P03:{p.Name}", "P03", "kritik", "proje", p.Name,
                            $"{p.Name}: bırakma tarihi {-d} gün önce geçti", p.OlumKriteri, "Bırak, beklet ya da kriteri güncelle; kararı yaz.",
                            new ProjectTarget(p.Name, "genel"), null));
                    else if (d <= 7)
                        list.Add(new ProblemDto($"P03:{p.Name}", "P03", "dikkat", "proje", p.Name,
                            $"{p.Name}: bırakma tarihine {d} gün kaldı", p.OlumKriteri, "Kriteri karşılamak için ne gerektiğine bak.",
                            new ProjectTarget(p.Name, "genel"), null));
                }
                else if (active)
                    list.Add(new ProblemDto($"P03:{p.Name}:yok", "P03", "dikkat", "proje", p.Name,
                        $"{p.Name}: ölüm kriterinde tarih yok", "", "\"olum_kriteri\" alanına bir YYYY-MM-DD tarihi ekle.",
                        new NoteTarget(cardPath, null), null));
            }

            // P04 — stale update
            if (!ended)
            {
                DateOnly? last = p.SonGuncelleme;
                if (kayitByProject.TryGetValue(p.Name, out var entries))
                {
                    var newest = entries.Where(e => e.Kind == "devir").Select(e => e.Date).DefaultIfEmpty().Max();
                    if (newest != default && (last is null || newest > last)) last = newest;
                }
                if (last is { } l)
                {
                    int ageDays = today.DayNumber - l.DayNumber;
                    if (active && ageDays > 14)
                        list.Add(new ProblemDto($"P04:{p.Name}", "P04", "dikkat", "proje", p.Name, $"{p.Name} {ageDays} gündür güncellenmedi",
                            "", "Kısa bir devir notu yaz ya da \"son_guncelleme\"yi güncelle.", new ProjectTarget(p.Name, "genel"), null));
                    else if (!active && ageDays > 30)
                        list.Add(new ProblemDto($"P04:{p.Name}", "P04", "bilgi", "proje", p.Name, $"{p.Name} {ageDays} gündür güncellenmedi",
                            "", "Hâlâ gerekli mi diye bak.", new ProjectTarget(p.Name, "genel"), null));
                }
                else
                    list.Add(new ProblemDto($"P04:{p.Name}:yok", "P04", "bilgi", "proje", p.Name, $"{p.Name}: güncelleme tarihi yok",
                        "", "\"son_guncelleme\" alanını doldur.", new NoteTarget(cardPath, null), null));
            }

            // P05 — stale lock
            if (!string.IsNullOrWhiteSpace(p.Kilit))
            {
                var (holder, since) = ParseLock(p.Kilit);
                if (since is { } s)
                {
                    double hours = (now - s).TotalHours;
                    if (hours > 12)
                        list.Add(new ProblemDto($"P05:{p.Name}", "P05", "dikkat", "not", p.Name,
                            $"{p.Name} kilidi {(int)hours} saattir duruyor ({holder})", "", "Kilidi kaldır ya da işi bitir.",
                            new NoteTarget(cardPath, null), s.ToString("o", CultureInfo.InvariantCulture)));
                }
                else
                    list.Add(new ProblemDto($"P05:{p.Name}:okunamadi", "P05", "bilgi", "not", p.Name,
                        $"{p.Name}: kilit alanı okunamadı ({p.Kilit})", "", "\"kilit: <araç> <YYYY-MM-DD HH:MM>\" biçimini kullan.",
                        new NoteTarget(cardPath, null), null));
            }
        }

        // P06 — stuck tasks
        foreach (var t in snap.Tasks)
        {
            int ageDays = (int)(now - t.Modified).TotalDays;
            if (t.Durum is "verildi" or "kontrol" && ageDays > 2)
                list.Add(new ProblemDto($"P06:{t.RelPath}", "P06", "dikkat", "gorev", t.Project,
                    $"{t.Kimlik} {ageDays} gündür \"{t.Durum}\" durumunda", "", "İlerlemeyi kontrol et ya da durumu güncelle.",
                    new NoteTarget(t.RelPath, null), null));
            else if (t.SonTarih is { } due && due < today && t.Durum is not ("tamam" or "iptal"))
                list.Add(new ProblemDto($"P06:{t.RelPath}:tarih", "P06", "dikkat", "gorev", t.Project,
                    $"{t.Kimlik}: son tarihi geçti ({due:yyyy-MM-dd})", "", "Yeni bir tarih ver ya da işi bitir/iptal et.",
                    new NoteTarget(t.RelPath, null), null));
        }

        // P07 — open items from the latest handoff
        foreach (var p in snap.Projects.Where(p => p.Durum is "aktif" or "beklemede"))
        {
            if (!kayitByProject.TryGetValue(p.Name, out var entries)) continue;
            var latest = KayitParser.LatestDevir(entries);
            string? acik = latest?.Get("Açık kalan");
            if (acik is null) continue;
            string norm = acik.Trim().TrimEnd('.').ToLowerInvariant();
            if (norm is "yok" or "-" or "") continue;
            list.Add(new ProblemDto($"P07:{p.Name}:{latest!.Anchor}", "P07", "bilgi", "proje", p.Name,
                $"{p.Name}: son devirde açık kalanlar", acik, "Bir sonraki oturumda devam et.",
                new NoteTarget(latest.Path, latest.Anchor), null));
        }

        // P08 — pending safety requests
        foreach (var s in snap.SafetyPending)
            list.Add(new ProblemDto($"P08:{s.Id}", "P08", s.Critical ? "kritik" : "dikkat", "guvenlik", null,
                $"{s.ToolName} onayını bekliyor: {s.Islem}", s.Hedef, "Masam'da Onayla/Reddet.",
                new PageTarget("masam", null), s.Zaman?.ToString("o", CultureInfo.InvariantCulture)));

        // P09 — a Claude session may be waiting for permission (best-effort: app-started sessions are not tracked here yet)
        foreach (var cs in snap.ClaudeSessions.Where(c => c.State == ClaudeSessionState.IzinBekliyorOlabilir))
            list.Add(new ProblemDto($"P09:{cs.SessionId}", "P09", "dikkat", "calisan", null,
                $"Bir Claude oturumu izin bekliyor olabilir: {cs.Title}", "", "Claude'u aç ve oturumu kontrol et.",
                new PageTarget("sirketim", null), cs.LastActivityLocal.ToString("o", CultureInfo.InvariantCulture)));

        // P10 — app job states
        foreach (var j in jobs)
        {
            if (j.State == "giris")
                list.Add(new ProblemDto($"P10:{j.Id}:giris", "P10", "kritik", "calisan", j.Project,
                    "Claude'a giriş gerekli", j.Fix ?? "", j.Fix ?? "Claude komut satırında bir kez /login yap.", new JobTarget(j.Id), null));
            else if (j.State == "kota")
                list.Add(new ProblemDto($"P10:{j.Id}:kota", "P10", "dikkat", "calisan", j.Project,
                    $"{(j.Tool == "codex" ? "Codex" : "Claude")} kotası doldu", "", "Biraz sonra yeniden dene.", new JobTarget(j.Id), null));
            else if (j.State == "hata" && j.End is not null)
                list.Add(new ProblemDto($"P10:{j.Id}:hata", "P10", "dikkat", "calisan", j.Project,
                    $"Bir {(j.Tool == "codex" ? "Codex" : "Claude")} işi hatayla bitti", j.ResultPreview ?? "", "Detayına bak.",
                    new JobTarget(j.Id), null));
            else if (j.Denials.Count > 0)
                list.Add(new ProblemDto($"P10:{j.Id}:izin", "P10", "bilgi", "calisan", j.Project,
                    $"İzin verilmedi: {j.Denials[0]}", string.Join(", ", j.Denials), "Gerekirse elle yap ya da izinleri gözden geçir.",
                    new JobTarget(j.Id), null));
        }

        // P11 — Codex quota
        var kota = lights.FirstOrDefault(l => l.Id == "kota");
        if (kota is { State: "yellow" })
            list.Add(new ProblemDto("P11:kota", "P11", "bilgi", "sistem", null, $"Codex kotası {kota.Detail}'de açılıyor",
                "", "O saate kadar bekle ya da Claude kullan.", new PageTarget("sirketim", null), null));

        // P12 — active project limit / focus
        int activeCount = snap.ActiveCount;
        if (activeCount > 3)
            list.Add(new ProblemDto("P12:limit", "P12", "kritik", "sistem", null, $"{activeCount} aktif proje var, sınır 3",
                "", "Birini bitir, beklet ya da dondur.", new PageTarget("projeler", null), null));
        int focusCount = snap.Projects.Count(p => p.Odak && p.Durum == "aktif");
        if (activeCount > 0 && focusCount != 1)
            list.Add(new ProblemDto("P12:odak", "P12", "dikkat", "sistem", null,
                focusCount == 0 ? "Odak projesi yok" : "Birden fazla odak projesi var", "", "Tek bir aktif projeyi odak yap.",
                new PageTarget("projeler", null), null));

        // P13 — decision pending
        foreach (var p in snap.DecisionPending)
            list.Add(new ProblemDto($"P13:{p.Name}", "P13", "dikkat", "proje", p.Name, $"{p.Name} senin kararını bekliyor",
                "", "Proje kartını aç ve karar ver.", new ProjectTarget(p.Name, "genel"), null));

        // P14 — emergency stop
        if (snap.Stopped)
            list.Add(new ProblemDto("P14:durdur", "P14", "kritik", "sistem", null, "Acil durdurma açık: yapay zekâlar çalışamıyor",
                "", "Sorun geçtiyse \"Devam Et\"e bas.", new PageTarget("masam", null), null));

        // P15 — infrastructure
        if (!snap.VaultExists)
            list.Add(new ProblemDto("P15:kasa", "P15", "kritik", "sistem", null, "Kasa klasörü bulunamadı", snap.Root,
                "Ayarlar'dan doğru klasörü seç.", new PageTarget("ayarlar", null), null));
        var kasaLight = lights.FirstOrDefault(l => l.Id == "kasa");
        // Kasa ışığı artık sadece kasa klasörü yoksa kırmızı (git'siz kasa sarı; o durum P15:uyari'deki "Kasa bir git
        // deposu değil" bilgisiyle zaten görünür). Klasör yokken P15:kasa yeterli, aynı sorunu iki kez sayma.
        if (kasaLight is { State: "red" } && snap.VaultExists)
            list.Add(new ProblemDto("P15:git", "P15", "kritik", "sistem", null, "Kasa ya da git çalışmıyor", kasaLight.Detail,
                "Git kurulu mu ve kasa bir git deposu mu, kontrol et.", new PageTarget("ayarlar", null), null));
        var gateLight = lights.FirstOrDefault(l => l.Id == "kapi");
        // Kapı yoksa: bu uygulamadan hiç yapay zekâ işi verilmediyse (sadece notlarını görüyorsan) bu bir kriz değil,
        // "dikkat"; iş verme kullanılıyorsa "kritik".
        if (gateLight is { State: "red" })
            list.Add(new ProblemDto("P15:kapi", "P15", jobs.Count > 0 ? "kritik" : "dikkat", "sistem", null, "Güvenlik kapısı kurulu değil", gateLight.Detail,
                "Yapay zekâya iş vermeden önce güvenlik kapısını kur (kasa-araclari/KURULUM.md, 3. adım).", new PageTarget("ayarlar", null), null));
        else if (gateLight is { State: "yellow" })
            list.Add(new ProblemDto("P15:kapi-kasa", "P15", "bilgi", "sistem", null, "Güvenlik kapısı bu kasayı kesin bilmiyor", gateLight.Detail,
                "IKINCI_BEYIN_KASA ortam değişkenini bu kasanın yoluna ayarla (kasa-araclari/KURULUM.md 3.2).", new PageTarget("ayarlar", null), null));
        foreach (var w in snap.Warnings.Take(5))
            list.Add(new ProblemDto($"P15:uyari:{w.GetHashCode():x}", "P15", "bilgi", "sistem", null, w, "", "Ayarlar'da ayrıntıya bak.",
                new PageTarget("ayarlar", null), null));

        // P16 — stale Park Yeri idea
        foreach (var n in index.Notes.Values.Where(n => n.Kind == "fikir"))
        {
            var tarih = n.Frontmatter.TryGetValue("tarih", out var tv) ? Md.FirstIsoDate(tv) : null;
            var when = tarih ?? DateOnly.FromDateTime(n.Mtime.ToLocalTime());
            if (today.DayNumber - when.DayNumber > 7)
                list.Add(new ProblemDto($"P16:{n.RelPath}", "P16", "bilgi", "fikir", null,
                    $"«{n.Title}» 7 günden uzun süredir Park Yeri'nde", "", "Değerlendir: yap, beklet ya da sil.",
                    new NoteTarget(n.RelPath, null), null));
        }

        // P17 — stale inbox item
        foreach (var n in index.Notes.Values.Where(n => n.Kind == "gelen"))
        {
            int ageDays = today.DayNumber - DateOnly.FromDateTime(n.Mtime.ToLocalTime()).DayNumber;
            if (ageDays > 3)
                list.Add(new ProblemDto($"P17:{n.RelPath}", "P17", "bilgi", "not", null, $"Gelen kutusunda bekleyen: «{n.Title}»",
                    "", "Bir yere taşı ya da işle.", new NoteTarget(n.RelPath, null), null));
        }

        return Sort(list);
    }

    static List<ProblemDto> Sort(List<ProblemDto> list) =>
        list.OrderBy(p => SeverityRank(p.Severity)).ThenBy(p => p.Project ?? "", StringComparer.Create(Md.Tr, true))
            .ThenBy(p => p.Title, StringComparer.Create(Md.Tr, true)).ToList();

    static int SeverityRank(string s) => s switch { "kritik" => 0, "dikkat" => 1, _ => 2 };

    /// <summary>"claude 2026-09-23 22:10" → (holder, when). Unparseable → (raw, null).</summary>
    public static (string holder, DateTime? since) ParseLock(string kilit)
    {
        string s = kilit.Trim();
        int sp = s.IndexOf(' ');
        if (sp < 0) return (s, null);
        string holder = s[..sp];
        string rest = s[(sp + 1)..].Trim();
        return DateTime.TryParse(rest, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? (holder, dt) : (holder, null);
    }

    /// <summary>bekleyen bir güvenlik onayı (P08) bir şey bozuk olduğu için
    /// değil, kararın zamana duyarlı olduğu için "kritik"tir; kullanıcının karar vereceği 4 bekleyen silme isteği tek
    /// başına sağlık puanını 0'a çekmemeli. Bu yüzden SADECE puan/seviye formülünde P08, kayıtlı
    /// <see cref="ProblemDto.Severity"/> değeri "kritik" olsa bile "dikkat" ağırlığıyla sayılır (alanın kendisi
    /// değişmez; Masam kartı, süzgeçler ve başka yerlerdeki sayılar onu hâlâ acil gösterir). Gerçek bir kriz (P03
    /// bırakma tarihi geçti, P12 proje sınırı, P14 Acil Durdur, P15 bozuk altyapı, …) "risk" seviyesini eskisi gibi
    /// zorlar.</summary>
    public static (string level, int score) ComputeHealth(IEnumerable<ProblemDto> problems)
    {
        int k = 0, d = 0, b = 0;
        foreach (var p in problems)
        {
            bool pendingApproval = p.Rule == "P08";
            if (p.Severity == "kritik" && !pendingApproval) k++;
            else if (p.Severity == "dikkat" || (p.Severity == "kritik" && pendingApproval)) d++;
            else b++;
        }
        int score = Math.Clamp(100 - 25 * k - 6 * d - 1 * b, 0, 100);
        string level = k > 0 ? "risk" : score >= 80 ? "iyi" : score >= 50 ? "dikkat" : "risk";
        return (level, score);
    }

    /// <summary>One-line explanation for the formula above, shown next to the health score (empty when it made no
    /// difference). Pure text, no i18n needs beyond Turkish.</summary>
    public static string HealthHint(IEnumerable<ProblemDto> problems)
    {
        int pending = problems.Count(p => p.Rule == "P08" && p.Severity == "kritik");
        if (pending == 0) return "";
        string adet = pending == 1 ? "1 onay bekleyen kritik istek" : $"{pending} onay bekleyen kritik istek";
        return $"Puana {adet} \"dikkat\" ağırlığıyla girdi; tek başına puanı sıfırlamaz.";
    }
}
