// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
// Uydurma örnek veri: depodaki ornek-kasa ile tutarlı (Kişisel Blog, Hava Durumu Uygulaması, Portfolyo Yenileme,
// Mobil Not Defteri, Eski Blog Taşıma). Gerçek kişi, şirket, proje ya da dosya yolu içermez.
// Plain ES module, no DOM — importable from Node for the screenshot/compare scripts.
import { IMZA } from "./imza.js";

export const TODAY = "2026-09-24";
export const TODAY_TEXT = "24 Eylül 2026, Perşembe";

function iso(day, time) { return `${day}T${time || "12:00:00"}+03:00`; }
function addDays(day, n) {
  const d = new Date(day + "T00:00:00Z");
  d.setUTCDate(d.getUTCDate() + n);
  return d.toISOString().slice(0, 10);
}
const TR_MONTHS = ["Oca","Şub","Mar","Nis","May","Haz","Tem","Ağu","Eyl","Eki","Kas","Ara"];
function trLabel(day) { const d = new Date(day + "T00:00:00Z"); return `${d.getUTCDate()} ${TR_MONTHS[d.getUTCMonth()]}`; }

// ---------------- Roller (ornek-kasa/60 Ekip/Roller içindeki 5 uydurma rol) ----------------
export const ROLES = [
  { title: "Kod Gözden Geçirici", cagri: "kod-gozden-gecirici", departman: "Mühendislik", yonetici: "Ben", arac: "Codex", model: "Codex", ozet: "Yazılan kodu okur, hataları ve basitleştirme fırsatlarını işaretler.", path: "60 Ekip/Roller/Kod Gözden Geçirici.md" },
  { title: "Test Asistanı", cagri: "test", departman: "Mühendislik", yonetici: "Ben", arac: "Claude alt ajanı", model: "Claude", ozet: "Yeni özellikleri denemeye çalışır, bulduğu hataları not eder.", path: "60 Ekip/Roller/Test Asistanı.md" },
  { title: "Tasarım Danışmanı", cagri: "tasarim", departman: "Tasarım", yonetici: "Ben", arac: "Claude alt ajanı", model: "Claude", ozet: "Renk, tipografi ve sayfa düzeni önerileri verir.", path: "60 Ekip/Roller/Tasarım Danışmanı.md" },
  { title: "Metin Üretici", cagri: "metin", departman: "İçerik", yonetici: "Ben", arac: "Claude alt ajanı", model: "Claude", ozet: "Blog yazıları, ürün açıklamaları ve sosyal medya metinleri hazırlar.", path: "60 Ekip/Roller/Metin Üretici.md" },
  { title: "Yayın Planlayıcı", cagri: "yayin", departman: "İçerik", yonetici: "Ben", arac: "Claude alt ajanı", model: "Claude", ozet: "Blog ve sosyal medya paylaşım takvimini hazırlar.", path: "60 Ekip/Roller/Yayın Planlayıcı.md" },
];
export const DEPARTMENTS = ["Mühendislik", "Tasarım", "İçerik"];

// ---------------- Projects ----------------
export const PROJECTS = [
  {
    name: "Kişisel Blog", path: "20 Projeler/Kişisel Blog/Kişisel Blog.md", folder: "Kişisel Blog",
    durum: "aktif", odak: true,
    bittiTanimi: "10 yazı yayında; yorum bölümü çalışıyor; basit istatistik sayfası var",
    sonrakiAdim: "Eğer bu akşam 30 dk ayırırsam → yorum bölümü ekleyip test ederim",
    sonrakiAdimKisa: "Yorum bölümü eklenip test edilecek (30 dk)",
    olumKriteri: "Eğer 31 Ekim'e kadar 10 yazı yayınlanmazsa → tempo düşürülür",
    killDate: "2026-10-31", daysToKill: 37, kararBekliyor: false,
    kilit: null, baslangic: "2026-08-20", sonGuncelleme: "2026-09-24", daysSinceUpdate: 0,
    bitti: { done: 1, total: 3, pct: 33, items: [
      { done: false, text: "10 yazı yayında" },
      { done: false, text: "Yorum bölümü çalışıyor" },
      { done: true, text: "Basit istatistik sayfası var" },
    ] },
    engel: "Zamanı düzenli ayırmak; yazı temposu bu yüzden yavaş.",
    beklenenler: ["Bu akşam 30 dk: yorum bölümünü ekle ve dene", "Haftada birkaç kez kısa geri bildirim ver"],
    klasorVar: false,
    health: { level: "dikkat", score: 64, problemCount: 2 },
  },
  {
    name: "Hava Durumu Uygulaması", path: "20 Projeler/Hava Durumu Uygulaması/Hava Durumu Uygulaması.md", folder: "Hava Durumu Uygulaması",
    durum: "aktif", odak: false,
    bittiTanimi: "5 günlük tahmin ekranı; widget desteği; mağaza girişi tamam",
    sonrakiAdim: "Eğer yarın 45 dk ayırırsam → ikon setini uygularım",
    sonrakiAdimKisa: "İkon seti uygulanacak (45 dk)",
    olumKriteri: "Eğer 15 Kasım'a kadar mağazaya girmezse → yan projeye alınır",
    killDate: "2026-11-15", daysToKill: 52, kararBekliyor: false,
    kilit: null, baslangic: "2026-08-20", sonGuncelleme: "2026-09-24", daysSinceUpdate: 0,
    bitti: { done: 1, total: 3, pct: 33, items: [
      { done: true, text: "5 günlük tahmin ekranı" },
      { done: false, text: "Widget desteği" },
      { done: false, text: "Mağaza girişi tamam" },
    ] },
    engel: "Zamanı düzenli ayırmak.",
    beklenenler: ["İkon setini seç (3 aday hazır)"],
    klasorVar: true,
    health: { level: "iyi", score: 88, problemCount: 0 },
  },
  {
    name: "Portfolyo Yenileme", path: "20 Projeler/Portfolyo Yenileme/Portfolyo Yenileme.md", folder: "Portfolyo Yenileme",
    durum: "aktif", odak: false,
    bittiTanimi: "Ana sayfa yenilendi; proje kartları bölümü; iletişim formu",
    sonrakiAdim: "Eğer bu hafta sonu 1 saat ayırırsam → proje kartları bölümünü bitiririm",
    sonrakiAdimKisa: "Proje kartları bölümü bitirilecek (1 sa)",
    olumKriteri: "Eğer 30 Kasım'a kadar bitmezse → mevcut site kalır",
    killDate: "2026-11-30", daysToKill: 67, kararBekliyor: false,
    kilit: { holder: "claude", since: iso("2026-09-24", "00:00:00"), hours: 14, stale: true },
    baslangic: "2026-08-20", sonGuncelleme: "2026-09-23", daysSinceUpdate: 1,
    bitti: { done: 1, total: 3, pct: 33, items: [
      { done: true, text: "Ana sayfa yenilendi" },
      { done: false, text: "Proje kartları bölümü" },
      { done: false, text: "İletişim formu" },
    ] },
    engel: "Sürekli yeni tasarım denemek isteği; yeni deneme değil bitirmek bu projenin başarısı.",
    beklenenler: ["Proje kartları için renk önerisini onayla", "Hafta sonu 1 saat ayır, kartları bitir"],
    klasorVar: true,
    health: { level: "dikkat", score: 66, problemCount: 2 },
  },
  {
    name: "Mobil Not Defteri", path: "20 Projeler/Mobil Not Defteri/Mobil Not Defteri.md", folder: "Mobil Not Defteri",
    durum: "beklemede", odak: false,
    bittiTanimi: "Not ekle/sil; senkronizasyon; karanlık tema",
    sonrakiAdim: "Eğer Kişisel Blog bitince zaman kalırsa → senkronizasyonu tamamlarım",
    sonrakiAdimKisa: "Kişisel Blog bitince yeniden değerlendirilecek",
    olumKriteri: "Beklemede. Eğer Aralık'a kadar geri dönülmezse → dondurulur",
    killDate: "2026-12-01", daysToKill: 68, kararBekliyor: true,
    kilit: null, baslangic: "2026-08-20", sonGuncelleme: "2026-09-20", daysSinceUpdate: 4,
    bitti: { done: 1, total: 3, pct: 33, items: [
      { done: true, text: "Not ekle/sil" },
      { done: false, text: "Senkronizasyon" },
      { done: false, text: "Karanlık tema" },
    ] },
    engel: "Şu an odak başka projede; bilinçli bekletme.",
    beklenenler: ["Karar bekliyor: Mobil Not Defteri'ne ne zaman geri dönülsün?"],
    klasorVar: false,
    health: { level: "dikkat", score: 71, problemCount: 1 },
  },
  {
    name: "Eski Blog Taşıma", path: "20 Projeler/Eski Blog Taşıma/Eski Blog Taşıma.md", folder: "Eski Blog Taşıma",
    durum: "bitti", odak: false,
    bittiTanimi: "Tüm yazılar taşındı; eski bağlantılar yönlendirildi; eski site kapatıldı",
    sonrakiAdim: "Yok, proje bitti.",
    sonrakiAdimKisa: "Proje bitti",
    olumKriteri: "Bitti.",
    killDate: null, daysToKill: null, kararBekliyor: false,
    kilit: null, baslangic: "2026-08-20", sonGuncelleme: "2026-09-18", daysSinceUpdate: 6,
    bitti: { done: 3, total: 3, pct: 100, items: [
      { done: true, text: "Tüm yazılar taşındı" },
      { done: true, text: "Eski bağlantılar yönlendirildi" },
      { done: true, text: "Eski site kapatıldı" },
    ] },
    engel: "Yok, proje bitti.",
    beklenenler: [], klasorVar: false,
    health: { level: "iyi", score: 100, problemCount: 0 },
  },
];

// ---------------- Todos (Masam) ----------------
export const TODOS = [
  { key: "td-9-3f2a9b10", text: "Kişisel Blog için yeni yazı taslağı hazırlandı (23 Eylül)", done: true },
  { key: "td-10-77c1e004", text: "Hava Durumu Uygulaması: API anahtarı test edildi", done: true },
  { key: "td-16-a1b2c3d4", text: "Bugün 20 dk: Portfolyo Yenileme'de proje kartları bölümünü bitir", done: false },
  { key: "td-17-b5e6f708", text: "Hava Durumu Uygulaması için ikon seti seç", done: false },
  { key: "td-18-c9d0e1f2", text: "Kişisel Blog yorum bölümü için spam filtresi araştır", done: false },
  { key: "td-19-d3e4f5a6", text: "5 dk: İş Başvuruları listesine bak, başvurmak istediklerinin numarasını söyle", done: false },
];

// ---------------- Safety ----------------
// summary (A2, matches Bridge/Dto.cs SafetyDto.Summary): the card is bound to the exact bekleyenler.jsonl
// record via this short hash; a mismatched `summary` on decideSafety means the queue changed underneath
// the card ("değişmiş, onaylanamaz"). The bridge has no separate short/long command fields — `komut` is
// always the FULL text; the page itself decides whether to shorten it for display (masam.js KOMUT_KISA_LIMIT).
function hash8(s) { let h = 2166136261; for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 16777619); } return (h >>> 0).toString(16).padStart(8, "0"); }
export const SAFETY = [
  {
    id: "20260924-1f026c", time: iso("2026-09-24", "00:12:00"), tool: "Claude",
    islem: "Eski yayın klasörünü sil", hedef: "Portfolyo\\yayin-eski",
    komut: "Remove-Item -Recurse -Force \"Portfolyo\\yayin-eski\"",
    summary: hash8("Remove-Item -Recurse -Force \"Portfolyo\\yayin-eski\""),
    risk: "Geri dönüşü yok ama sadece eski bir yedek klasörü.", critical: false,
    neden: "Yeni ana sayfa yayınlanınca artık kullanılmayan eski derleme klasörünü temizlemek istiyor.",
    kesildi: false, jobId: null,
  },
  {
    id: "20260923-9a7710", time: iso("2026-09-23", "23:41:00"), tool: "Codex",
    islem: "Hava Durumu Uygulaması klasörünü sil", hedef: "Projeler\\HavaDurumu\\",
    komut: "Remove-Item -Recurse -Force \"Projeler\\HavaDurumu\" # bu betik önce .bak yedeği alır, sonra klasörü ve alt klasörlerini kalıcı olarak siler; geri alma yalnızca .bak'tan elle mümkündür.",
    summary: hash8("Remove-Item -Recurse -Force \"Projeler\\HavaDurumu\" (tam komut)"),
    risk: "Geri dönüşü yok, gerçek proje klasörü — kritik.", critical: true,
    neden: "Bağlamı yanlış anlamış görünüyor; hedef proje kartıyla ilgisiz.",
    kesildi: false, jobId: null,
  },
];

// ---------------- Applications ----------------
// Şirketler ve ilanlar uydurmadır (ornek-kasa/40 Alanlar/İş Başvuruları.md ile aynı adlar).
export const APPLICATIONS = {
  found: true, awaitingCount: 2, docsDirExists: true,
  sections: [
    { name: "Başvurulacaklar", columns: ["No","Firma","Pozisyon","Tür","Kanal","Şehir","Uygunluk","Eksik beceri","İlan linki","Belgeler","Durum","Not"], rows: [
      { key: "ap-4-1a2b3c4d", section: "Başvurulacaklar", no: "1", firma: "Örnek Yazılım A.Ş.", pozisyon: "Junior Frontend Geliştirici", tur: "Tam zamanlı", kanal: "İlan sitesi", sehir: "İstanbul (uzaktan)", uygunluk: "Yüksek", eksikBeceri: "—", url: "https://example.com/ilan-1", belgeler: "CV.pdf", durum: "Onay bekliyor", durumKind: "onay-bekliyor", not: "Portfolyodaki projelerle örtüşüyor.", awaiting: true },
      { key: "ap-5-2b3c4d5e", section: "Başvurulacaklar", no: "2", firma: "Deneme Teknoloji", pozisyon: "Stajyer Geliştirici", tur: "Staj", kanal: "İlan sitesi", sehir: "Ankara", uygunluk: "Orta", eksikBeceri: "Docker", url: "https://example.com/ilan-2", belgeler: "CV.pdf, ön yazı.pdf", durum: "Onay bekliyor", durumKind: "onay-bekliyor", not: "Okul programıyla uyumlu saatler.", awaiting: true },
      { key: "ap-6-3c4d5e6f", section: "Başvurulacaklar", no: "3", firma: "Ufuk Dijital", pozisyon: "Yarı Zamanlı Geliştirici", tur: "Yarı zamanlı", kanal: "Şirket sitesi", sehir: "İzmir", uygunluk: "Düşük", eksikBeceri: "3+ yıl deneyim", url: "https://example.com/ilan-3", belgeler: "—", durum: "Olumsuz", durumKind: "olumsuz", not: "Deneyim şartı tutmuyor.", awaiting: false },
    ] },
    { name: "Başvuruldu", columns: ["No","Firma","Pozisyon","Tür","Kanal","Şehir","Uygunluk","Eksik beceri","İlan linki","Belgeler","Durum","Not"], rows: [
      { key: "ap-7-4d5e6f70", section: "Başvuruldu", no: "1", firma: "Parlak Yazılım", pozisyon: "Stajyer", tur: "Staj", kanal: "Şirket sitesi", sehir: "Bursa", uygunluk: "Orta", eksikBeceri: "—", url: "https://example.com/ilan-4", belgeler: "CV.pdf, ön yazı.pdf", durum: "Başvuruldu", durumKind: "basvuruldu", not: "Yanıt bekleniyor.", awaiting: false },
    ] },
  ],
};

// ---------------- Jobs ----------------
// Anchored to the real clock (not TODAY) so the "kota-bekliyor" countdown always shows a believable
// remaining time, whenever this mock is actually loaded/screenshotted.
function inFuture(ms) { return new Date(Date.now() + ms).toISOString(); }
function inPast(ms) { return new Date(Date.now() - ms).toISOString(); }
function hhmm(isoStr) { const d = new Date(isoStr); return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`; }
const KOTA_RESUME_AT = inFuture(2 * 3600000 + 14 * 60000);
export const JOBS = [
  {
    id: "claude:20260924-013000-000-kota0001", tool: "claude", title: "Widget kodunu sadeleştir",
    prompt: "Kod Gözden Geçirici: Hava Durumu widget kodundaki gereksiz yeniden çizimleri bul, gerekiyorsa sadeleştir.",
    role: "Kod Gözden Geçirici", project: "Hava Durumu Uygulaması",
    state: "kota-bekliyor", stateText: `Kota dolu — ${hhmm(KOTA_RESUME_AT)}'de kendiliğinden devam edecek`, tone: "yellow",
    start: inPast(3 * 3600000), end: null, elapsedSec: 3600, lastActivity: inPast(70 * 60000),
    now: "Kota doldu, bekleniyor", steps: 12, filesChanged: [], denials: [], safetyIds: [],
    quiet: false, exitCode: null, resultPreview: null, fix: null, canCancel: true,
    resumeAt: KOTA_RESUME_AT, attempt: 2,
  },
  {
    id: "claude:20260924-000512-231-7a1b2c3d", tool: "claude", title: "Portfolyo proje kartları bileşenini kur",
    prompt: "Tasarım Danışmanı: Portfolyo Yenileme için proje kartları bölümünü kur, renk ve ikon önerilerini uygula.",
    role: "Tasarım Danışmanı", project: "Portfolyo Yenileme",
    state: "calisiyor", stateText: "Çalışıyor", tone: "teal",
    start: iso("2026-09-24", "00:05:12"), end: null, elapsedSec: 1380, lastActivity: iso("2026-09-24", "00:27:41"),
    now: "Düzenliyor: portfolyo/src/components/ProjeKarti.js",
    steps: 42, filesChanged: ["portfolyo/src/index.html", "portfolyo/src/css/site.css"], denials: [], safetyIds: [],
    quiet: false, exitCode: null, resultPreview: null, fix: null, canCancel: true, resumeAt: null, attempt: 1,
  },
  {
    id: "claude:20260923-214003-118-9c0d1e2f", tool: "claude", title: "Yorum formu testlerini çalıştır",
    prompt: "Test Asistanı: Kişisel Blog yorum formunu baştan sona dene, bulduğun hataları raporla.",
    role: "Test Asistanı", project: "Kişisel Blog",
    state: "giris", stateText: "Giriş gerekli", tone: "red",
    start: iso("2026-09-23", "21:40:03"), end: iso("2026-09-23", "21:40:19"), elapsedSec: 16, lastActivity: iso("2026-09-23", "21:40:19"),
    now: "Durdu", steps: 1, filesChanged: [], denials: [], safetyIds: [],
    quiet: false, exitCode: 1, resultPreview: "OAuth token has expired. Please run /login",
    fix: "Claude komut satırında bir kez /login yap.", canCancel: false, resumeAt: null, attempt: 1,
  },
  {
    id: "codex:20260923-193355-047-5f6a7b8c", tool: "codex", title: "G-001: yorum bölümü testi ve düzeltme",
    prompt: "Kişisel Blog için G-001 yorum bölümü görevini bağımsız dene ve bulunan hataları düzelt.",
    role: null, project: "Kişisel Blog",
    state: "bitti", stateText: "Bitti", tone: "green",
    start: iso("2026-09-23", "19:33:55"), end: iso("2026-09-23", "20:11:02"), elapsedSec: 2227, lastActivity: iso("2026-09-23", "20:11:02"),
    now: "Tamamlandı", steps: 58, filesChanged: ["blog/src/yorumlar.js"], denials: [], safetyIds: [],
    quiet: false, exitCode: 0, resultPreview: "48 otomatik kontrol geçti, 1 güvenlik bulgusu düzeltildi.", fix: null, canCancel: false, resumeAt: null, attempt: 1,
  },
];

// ---------------- History (40 items over 5 days) ----------------
function buildHistory() {
  const items = [];
  const days = [0, -1, -2, -3, -4].map((n) => addDays(TODAY, n));
  const templates = [
    { kind: "commit", tool: "claude", text: "[claude] Kişisel Blog yorum formu eklendi" },
    { kind: "commit", tool: "codex", text: "[codex] G-001 yorum bölümü düzeltmeleri" },
    { kind: "istek", tool: "claude", text: "Portfolyo'da proje kartlarının daha canlı olmasını istedi" },
    { kind: "istek", tool: "codex", text: "Yorum formu kodunu gözden geçir dedi" },
    { kind: "gorev", tool: null, text: "G-001 görevi tamam olarak işaretlendi" },
    { kind: "isaret", tool: null, text: "Kişisel Blog için yeni yazı taslağı hazırlandı işaretlendi" },
    { kind: "is", tool: "codex", text: "Codex işi bitti: çıkış 0" },
    { kind: "devir", tool: "claude", text: "Son devir: yorum formu eklendi, spam filtresi araştırılıyor" },
    { kind: "karar", tool: "claude", text: "KARAR: proje kartları tek bileşen olarak yazılacak" },
  ];
  let n = 0;
  for (const day of days) {
    const count = day === TODAY ? 10 : 7 + (n % 3);
    for (let i = 0; i < count; i++) {
      const t = templates[(n + i) % templates.length];
      n++;
      items.push({
        time: iso(day, `${String(9 + (i % 10)).padStart(2, "0")}:${String((i * 7) % 60).padStart(2, "0")}:00`),
        day, kind: t.kind, tool: t.tool,
        text: t.text + (i % 4 === 0 ? "" : ""),
        project: ["Kişisel Blog", "Hava Durumu Uygulaması", "Portfolyo Yenileme", null][i % 4],
        target: null,
      });
    }
  }
  items.sort((a, b) => (a.time < b.time ? 1 : -1));
  return items.slice(0, 40);
}
export const HISTORY = buildHistory();

// ---------------- LinkedIn (uydurma yazı başlıkları) ----------------
export const LINKEDIN = {
  found: true,
  published: [{ no: "1", tarih: "2026-09-23", konu: "Kişisel blogumu neden yeniden açtım" }],
  drafts: ["Hava durumu uygulamasından öğrendiklerim", "Portfolyo yenilerken dikkat ettiğim 5 şey"],
};

// ---------------- Shortcuts ----------------
// lastRun (A13, new field): ISO of the last time this shortcut ran in this app, or null if never — shown
// as "Son çalışma: N önce" / "Henüz çalıştırılmadı" on the tile.
export const SHORTCUTS = [
  { id: "simdi", group: "ac", icon: "doc", title: "01 Şimdi'yi aç", text: "Obsidian'da bugünün listesini açar", lastRun: iso("2026-09-24", "09:02:00") },
  { id: "kasa", group: "ac", icon: "folder", title: "Kasayı aç", text: "İkinci beyin klasörünü Explorer'da açar", lastRun: iso("2026-09-23", "22:10:00") },
  { id: "claudeUygulama", group: "ac", icon: "message", title: "Claude'u aç", text: "Claude uygulamasını açar", lastRun: iso("2026-09-24", "00:05:00") },
  { id: "codexUygulama", group: "ac", icon: "cpu", title: "Codex'i aç", text: "Codex uygulamasını açar", lastRun: null },
  { id: "linkedin", group: "ac", icon: "external", title: "LinkedIn'i aç", text: "Akışı tarayıcıda açar", lastRun: iso("2026-09-23", "19:50:00") },
  { id: "basvuruKlasoru", group: "ac", icon: "folder", title: "İş başvuru klasörü", text: "Belgelerin bulunduğu klasörü açar", lastRun: null },
  { id: "haftalik", group: "ac", icon: "calendar", title: "\"/haftalik\" kopyala + Claude'u aç", text: "Beceri kurulu değil (KURULUM.md 3.6)", lastRun: null, hazir: false },
  { id: "devir", group: "ac", icon: "doc", title: "\"/devir\" kopyala + Claude'u aç", text: "/devir becerisini panoya koyar", lastRun: iso("2026-09-23", "23:59:00"), hazir: true },
  { id: "istekTara", group: "bakim", icon: "search", title: "İstek defterini tara", text: "istek-defteri.mjs tara çalıştırır", lastRun: null },
  { id: "kapiTest", group: "bakim", icon: "shield", title: "Güvenlik kapısını dene", text: "Kapının kendi testini çalıştırır", lastRun: iso("2026-09-22", "14:00:00") },
];

// ---------------- Lights / usage / tools ----------------
export const LIGHTS = [
  { id: "claude", label: "Claude", state: "green", detail: "Çalışıyor", tooltip: "Claude Code CLI son 5 dakikada yanıt verdi." },
  { id: "codex", label: "Codex", state: "green", detail: "Bağlı", tooltip: "Codex MCP bağlantısı sağlıklı." },
  { id: "obsidian", label: "Obsidian", state: "yellow", detail: "Kapalı", tooltip: "Obsidian şu an açık değil; kasa yine de okunuyor." },
  { id: "kasa", label: "Kasa", state: "green", detail: "Senkron", tooltip: "Git deposu temiz, son commit bugün." },
  { id: "kapi", label: "Güvenlik kapısı", state: "green", detail: "Aktif", tooltip: "PreToolUse hook Claude ve Codex'te tanımlı." },
  { id: "kota", label: "Codex kotası", state: "green", detail: "Açık", tooltip: "Codex kullanım kotası dolu değil." },
  { id: "giris", label: "Claude girişi", state: "yellow", detail: "Gerekli", tooltip: "Son Claude işi oturum süresi dolduğu için durdu." },
];
export const USAGE = { codexTokens: 184000, claudeTokens: 96500, timedOut: false, codexText: "Bugün Codex ≈184 bin token kullandı.", claudeText: "Bugün Claude ≈96,5 bin token kullandı." };
export const TOOLS = {
  claudeHooks: ["PreToolUse: guvenlik-kapisi", "Stop: istek-defteri"],
  claudeAgents: ["Kod Gözden Geçirici", "Test Asistanı", "Tasarım Danışmanı"],
  // nextRun mirrors the real backend (Data/ToolStatusReader.NextRun): a ready-to-show Turkish phrase, never ISO
  // (the frontend must render it as is, not through fmtTime — see sirketim.js scheduledRow).
  // `name` is deliberately the raw scheduled-task folder slug here (what SKILL.md frontmatter actually holds
  // today), not a Turkish title — sirketim.js/ayarlar.js must run it through scheduledTaskLabel() (E).
  scheduledTasks: [
    { name: "aksam-analizi", description: "Her akşam günün kısa analizini kasaya yazar", nextRun: "bugün 21:30" },
    { name: "haftalık-gözden-geçirme", description: "Haftanın projelerini gözden geçirir, karar bekleyenleri Masam'a bırakır", nextRun: "27 Eyl Paz 19:00" },
    { name: "blog-yazı-taslağı", description: "Kişisel Blog için haftalık yazı taslağını hazırlar", nextRun: "26 Eyl Cmt" },
    { name: "tam-gaz", description: "Tam Gaz açıkken saatte bir çalışır", nextRun: "her saat (Tam Gaz açıkken) · sonraki 10:14" },
  ],
  codexMcp: ["filesystem", "git"], codexPlugins: [{ name: "kasa-kancalari", enabled: true }],
  codexHooks: ["istek-defteri", "guvenlik-kapisi"],
  vaultOk: true, gitOk: true, claudeSettingsFound: true, codexConfigFound: true,
};
export const CLAUDE_CLI = { found: true, version: "2.1.280", path: "%APPDATA%\\Claude\\claude-code\\2.1.280\\claude.exe", auth: "gerekli", loginCommand: "& \"$env:APPDATA\\Claude\\claude-code\\2.1.280\\claude.exe\"" };

// ---------------- Problems (covers every severity + several rules) ----------------
export const PROBLEMS = [
  { id: "P14:durdur", rule: "P14", severity: "bilgi", area: "sistem", project: null, title: "Acil durdurma kapalı, sistem normal çalışıyor", detail: "DURDUR dosyası yok.", fix: "Bir şey yapmana gerek yok.", target: { type: "page", page: "masam", tab: null }, since: null },
  { id: "P05:portfolyo", rule: "P05", severity: "dikkat", area: "proje", project: "Portfolyo Yenileme", title: "Portfolyo Yenileme kilidi 14 saattir duruyor (claude)", detail: "Kilit 00:00'da alındı, hâlâ açık.", fix: "Kilidi tutan oturumu bitir ya da kilidi elle temizle.", target: { type: "note", path: "20 Projeler/Portfolyo Yenileme/Portfolyo Yenileme.md", anchor: null }, since: iso("2026-09-24", "00:00:00") },
  { id: "P13:mobil-not", rule: "P13", severity: "dikkat", area: "proje", project: "Mobil Not Defteri", title: "Mobil Not Defteri senin kararını bekliyor", detail: "karar_bekliyor: true — Mobil Not Defteri'ne ne zaman geri dönülsün?", fix: "Beyin > Proje sayfasından karar ver.", target: { type: "project", name: "Mobil Not Defteri", section: "genel" }, since: null },
  { id: "P08:safety-critical", rule: "P08", severity: "dikkat", area: "guvenlik", project: null, title: "Codex onayını bekliyor: Hava Durumu Uygulaması klasörünü sil", detail: "Kritik bir silme isteği onay bekliyor.", fix: "Masam'daki onay kartından Reddet ya da Onayla.", target: { type: "page", page: "masam", tab: null }, since: iso("2026-09-23", "23:41:00") },
  { id: "P08:safety", rule: "P08", severity: "bilgi", area: "guvenlik", project: null, title: "Claude onayını bekliyor: Eski yayın klasörünü sil", detail: "Kritik olmayan bir silme isteği onay bekliyor.", fix: "Masam'daki onay kartından karar ver.", target: { type: "page", page: "masam", tab: null }, since: iso("2026-09-24", "00:12:00") },
  { id: "P09:izin", rule: "P09", severity: "dikkat", area: "calisan", project: null, title: "Bir Claude oturumu izin bekliyor olabilir: Yorum formu testleri", detail: "Oturum 21:40'tan beri yanıt vermiyor.", fix: "Şirketim > Çalışan işler'den oturumu kontrol et.", target: { type: "page", page: "sirketim", tab: null }, since: iso("2026-09-23", "21:40:19") },
  { id: "P10:giris", rule: "P10", severity: "kritik", area: "calisan", project: "Kişisel Blog", title: "Claude'a giriş gerekli", detail: "Son iş oturum süresi dolduğu için durdu.", fix: "Claude komut satırında bir kez /login yap.", target: { type: "job", id: "claude:20260923-214003-118-9c0d1e2f" }, since: iso("2026-09-23", "21:40:19") },
  { id: "P01:kayit", rule: "P01", severity: "dikkat", area: "not", project: "Portfolyo Yenileme", title: "«Kayıt» notunda 1 kırık bağlantı var", detail: "[[Eski Tasarım Notu]] bulunamadı.", fix: "Bağlantıyı düzelt ya da notu oluştur.", target: { type: "note", path: "20 Projeler/Portfolyo Yenileme/Kayıt.md", anchor: null }, since: null },
  { id: "P16:fikir", rule: "P16", severity: "bilgi", area: "fikir", project: null, title: "«Tarif defteri uygulaması» 9 gündür Park Yeri'nde", detail: "7 günden uzun süredir bekliyor.", fix: "Değerlendir ya da arşivle.", target: { type: "note", path: "30 Park Yeri/Tarif defteri uygulaması.md", anchor: null }, since: null },
  { id: "P17:inbox", rule: "P17", severity: "bilgi", area: "not", project: null, title: "Gelen kutusunda bekleyen: «Yorum bildirimleri fikri»", detail: "3 günden uzun süredir triaj edilmedi.", fix: "10 Gelen Kutusu'na bak, ilgili yere taşı.", target: { type: "note", path: "10 Gelen Kutusu/Yorum bildirimleri fikri.md", anchor: null }, since: null },
  { id: "P06:gorev", rule: "P06", severity: "dikkat", area: "gorev", project: "Kişisel Blog", title: "G-002 3 gündür \"kontrol\" durumunda", detail: "Dosya 3 gündür değişmedi.", fix: "Kontrolü bitir, tamam ya da iptal işaretle.", target: { type: "note", path: "20 Projeler/Kişisel Blog/Görevler/G-002 Spam filtresi araştırması.md", anchor: null }, since: null },
  { id: "P02:hava-durumu", rule: "P02", severity: "bilgi", area: "proje", project: "Hava Durumu Uygulaması", title: "Hava Durumu Uygulaması: karar_bekliyor alanı yazılmamış", detail: "Alan boş bırakılmış (bilgi amaçlı).", fix: "Kart alanını doldur ya da bilerek boş bırak.", target: { type: "note", path: "20 Projeler/Hava Durumu Uygulaması/Hava Durumu Uygulaması.md", anchor: null }, since: null },
];

// ---------------- Overview (Beyin > Genel bakış) ----------------
function buildCharts() {
  const days14 = Array.from({ length: 14 }, (_, i) => addDays(TODAY, i - 13));
  const labels14 = days14.map(trLabel);
  const claude14 = [3,2,4,1,5,2,3,4,2,6,3,5,4,5];
  const codex14 = [2,1,3,2,2,1,4,2,3,2,5,3,2,4];
  const commitVals = [1,0,2,1,3,1,2,2,1,3,2,4,2,3];
  const days30 = Array.from({ length: 30 }, (_, i) => addDays(TODAY, i - 29));
  const commits30 = days30.map((_, i) => (i % 7 === 0 ? 0 : 1 + ((i * 3) % 4)));
  const requests30 = days30.map((_, i) => 2 + ((i * 5) % 7));
  const kayit30 = days30.map((_, i) => (i % 4 === 0 ? 1 : 0));
  return {
    requests14: { days: days14, labels: labels14, claude: claude14, codex: codex14 },
    commits14: { days: days14, labels: labels14, values: commitVals },
    activity30: { days: days30, commits: commits30, requests: requests30, kayit: kayit30 },
    projectStatus: [
      { durum: "aktif", label: "Aktif", count: 3 },
      { durum: "beklemede", label: "Beklemede", count: 1 },
      { durum: "bitti", label: "Bitti", count: 1 },
      { durum: "donduruldu", label: "Donduruldu", count: 0 },
    ],
    taskFunnel: [
      { durum: "hazir", label: "Hazır", count: 2 },
      { durum: "verildi", label: "Verildi", count: 2 },
      { durum: "kontrol", label: "Kontrol", count: 1 },
      { durum: "tamam", label: "Tamam", count: 5 },
      { durum: "iptal", label: "İptal", count: 0 },
    ],
    progress: PROJECTS.filter(p => p.durum === "aktif").map(p => ({ project: p.name, done: p.bitti.done, total: p.bitti.total, pct: p.bitti.pct })),
    kill: PROJECTS.filter(p => p.killDate).map(p => ({ project: p.name, date: p.killDate, days: p.daysToKill, condition: p.olumKriteri })),
  };
}
const CHARTS = buildCharts();

export const OVERVIEW = {
  generatedAt: iso(TODAY, "00:41:00"),
  headline: [
    "24 Eylül 2026, Perşembe. 3 aktif proje var, sınır dolu; odak: Kişisel Blog (%33 bitti).",
    "Son 7 günde 14 kasa kaydı ve 19 istek oldu (geçen haftadan fazla).",
    "1 ciddi sorun, 6 uyarı var; en önemlisi: Claude'a giriş gerekli.",
    "2 şey onayını bekliyor; Masam'ın en üstünde.",
  ],
  // A10 (matches Brain/ProblemDetector.cs HealthHint): empty unless a pending-approval P08 problem is
  // counted as "dikkat" instead of zeroing the score — set here so genel.js's tooltip wiring gets exercised.
  health: { score: 71, level: "dikkat", counts: { kritik: 1, dikkat: 6, bilgi: 5 }, note: "Puana 1 onay bekleyen kritik istek \"dikkat\" ağırlığıyla girdi; tek başına puanı sıfırlamaz." },
  focus: { project: "Kişisel Blog", pct: 33, done: 1, total: 3 },
  week: { commits: 14, requests: 19, prevCommits: 9, prevRequests: 15 },
  kpis: [
    { id: "aktifProje", label: "Aktif proje", value: 3, display: "3/3", sub: "Sınır dolu", tone: "warn", tooltip: "Aynı anda en fazla 3 proje aktif olabilir; yenisi açılmaz.", trend: null, spark: null, progress: 100, target: { type: "page", page: "projeler", tab: null } },
    { id: "bugunYapilan", label: "Bugün yapılan", value: 2, display: "2/6", sub: "Yapılacaklarım", tone: "normal", tooltip: "Bugünkü Yapılacaklarım listesinden işaretlenenler.", trend: null, spark: null, progress: 33, target: { type: "page", page: "masam", tab: null } },
    { id: "haftaCommit", label: "Hafta kaydı", value: 14, display: "14", sub: "Pazartesiden beri", tone: "normal", tooltip: "Bu hafta kasaya yapılan commit sayısı.", trend: { delta: 5, text: "geçen haftadan +5" }, spark: [1,0,2,1,3,1,2,2,1,3,2,4,2,3], progress: null, target: { type: "page", page: "gecmis", tab: null } },
    { id: "haftaIstek", label: "Hafta isteği", value: 19, display: "19", sub: "Claude 11 · Codex 8", tone: "normal", tooltip: "Bu hafta Claude'a ve Codex'e yazdığın mesaj sayısı.", trend: { delta: 4, text: "geçen haftadan +4" }, spark: [3,2,4,1,5,2,3,4,2,6,3,5,4,5], progress: null, target: { type: "page", page: "gecmis", tab: null } },
    { id: "acikSorun", label: "Açık sorun", value: 7, display: "7", sub: "1 kritik · 6 dikkat", tone: "warn", tooltip: "Beyin'in otomatik bulduğu sorunlar (kritik + dikkat).", trend: null, spark: null, progress: null, target: { type: "page", page: "beyin", tab: "sorunlar" } },
    { id: "onayBekleyen", label: "Onay bekleyen", value: 2, display: "2", sub: "Masam'da gör", tone: "warn", tooltip: "Yapay zekâların onayını beklediği işlemler.", trend: null, spark: null, progress: null, target: { type: "page", page: "masam", tab: null } },
    { id: "seri", label: "Seri", value: 3, display: "3 gün", sub: "Kesme, devam ettir", tone: "normal", tooltip: "Art arda en az bir kasa kaydı ya da işaretlenen gün sayısı.", trend: null, spark: null, progress: null, target: null },
    { id: "linkedin", label: "LinkedIn", value: 1, display: "1", sub: "2 taslak bekliyor", tone: "normal", tooltip: "Yayınlanan LinkedIn gönderisi sayısı; taslaklar yayın takviminde onayını bekler.", trend: null, spark: null, progress: null, target: { type: "page", page: "basvurular", tab: null } },
    { id: "gelir", label: "Gelir", value: 0, display: "$0 / 100", sub: "İlk tahsilat bekleniyor", tone: "warn", tooltip: "Bu ay tahsil edilen tutar / 100 USD hedef.", trend: null, spark: null, progress: 0, target: { type: "page", page: "beyin", tab: "genel" } },
    { id: "kullanim", label: "Bugünkü kullanım", value: null, display: "≈281 bin token", sub: "Claude + Codex, tahmini", tone: "normal", tooltip: "Aboneliklerin dolar maliyeti yok; bu sadece hacim göstergesi.", trend: null, spark: null, progress: null, target: { type: "page", page: "ayarlar", tab: null } },
  ],
  projects: PROJECTS.filter(p => p.durum !== "bitti" && p.durum !== "donduruldu").map(p => ({
    name: p.name, durum: p.durum, odak: p.odak, level: p.health.level, score: p.health.score,
    reasons: p.health.problemCount > 0 ? [`${p.health.problemCount} açık sorun`] : ["Sorun yok"],
    bitti: p.bitti, daysToKill: p.daysToKill, daysSinceUpdate: p.daysSinceUpdate,
    lock: p.kilit, nextStep: p.sonrakiAdimKisa, lastHandoff: null, problemIds: PROBLEMS.filter(pr => pr.project === p.name).map(pr => pr.id),
  })),
  done: [
    { text: "Kişisel Blog için yeni yazı taslağı hazırlandı", project: "Kişisel Blog", source: "isaret", sourceLabel: "İşaretlendi", time: iso("2026-09-23","19:50:00"), target: null },
    { text: "Yorum bölümü: 48 otomatik kontrol geçti", project: "Kişisel Blog", source: "is", sourceLabel: "Yapay zekâ işi", time: iso("2026-09-23","20:11:02"), target: { type: "job", id: "codex:20260923-193355-047-5f6a7b8c" } },
    { text: "G-001: Yorum bölümü eklendi", project: "Kişisel Blog", source: "gorev", sourceLabel: "Görev tamam", time: iso("2026-09-22","16:20:00"), target: null },
    { text: "Son devir: ana sayfa yayına alındı", project: "Portfolyo Yenileme", source: "kayit", sourceLabel: "Devir notu", time: iso("2026-09-23","23:14:00"), target: { type: "note", path: "20 Projeler/Portfolyo Yenileme/Kayıt.md", anchor: "2026-09-23-23-14" } },
    { text: "Hava Durumu Uygulaması: API anahtarı test edildi", project: "Hava Durumu Uygulaması", source: "isaret", sourceLabel: "İşaretlendi", time: iso("2026-09-23","18:05:00"), target: null },
  ],
  notDone: [
    { text: "Bugün 20 dk: Portfolyo Yenileme'de proje kartları bölümünü bitir", project: null, source: "todo", sourceLabel: "Yapılacak", time: null, target: { type: "page", page: "masam", tab: null } },
    { text: "10 yazı yayında", project: "Kişisel Blog", source: "bitti", sourceLabel: "Bitti tanımı", time: null, target: { type: "project", name: "Kişisel Blog", section: "genel" } },
    { text: "Proje kartları bölümü", project: "Portfolyo Yenileme", source: "bitti", sourceLabel: "Bitti tanımı", time: null, target: { type: "project", name: "Portfolyo Yenileme", section: "genel" } },
    { text: "Karar bekliyor: Mobil Not Defteri'ne ne zaman geri dönülsün?", project: "Mobil Not Defteri", source: "beklenen", sourceLabel: "Senden beklenen", time: null, target: { type: "project", name: "Mobil Not Defteri", section: "genel" } },
    { text: "G-002 görevi 3 gündür kontrolde bekliyor", project: "Kişisel Blog", source: "gorev", sourceLabel: "Görev", time: null, target: null },
  ],
  recent: [
    { time: iso("2026-09-24","00:27:41"), kind: "devir", tool: "claude", title: "Portfolyo proje kartları üzerinde çalışılıyor", text: null, project: "Portfolyo Yenileme", target: null },
    { time: iso("2026-09-24","00:12:00"), kind: "onay", tool: "claude", title: "Onay bekliyor: eski yayın klasörünü sil", text: null, project: "Portfolyo Yenileme", target: { type: "page", page: "masam", tab: null } },
    { time: iso("2026-09-24","00:00:00"), kind: "karar", tool: "claude", title: "KARAR: proje kartları tek bileşen olarak yazılacak", text: null, project: "Portfolyo Yenileme", target: null },
    { time: iso("2026-09-23","23:41:00"), kind: "onay", tool: "codex", title: "Onay bekliyor: Hava Durumu Uygulaması klasörünü sil (kritik)", text: null, project: null, target: { type: "page", page: "masam", tab: null } },
    { time: iso("2026-09-23","23:14:00"), kind: "devir", tool: "codex", title: "Son devir: ana sayfa yayına alındı", text: null, project: "Portfolyo Yenileme", target: null },
    { time: iso("2026-09-23","21:40:19"), kind: "istek", tool: "claude", title: "Yorum formu testlerini çalıştır", text: null, project: "Kişisel Blog", target: null },
    { time: iso("2026-09-23","20:11:02"), kind: "is", tool: "codex", title: "Codex işi bitti: çıkış 0", text: null, project: "Kişisel Blog", target: { type: "job", id: "codex:20260923-193355-047-5f6a7b8c" } },
    { time: iso("2026-09-23","19:50:00"), kind: "commit", tool: "claude", title: "[claude] Yeni yazı taslağı kaydı işlendi", text: null, project: "Kişisel Blog", target: null },
  ],
  topProblems: PROBLEMS.slice(0, 8),
  charts: CHARTS,
  analysis: [
    "Odak projende (Kişisel Blog) 3 maddeden 1'i bitti; sıradaki adım yorum bölümünü eklemek.",
    "Son 7 günde Codex'e 8, Claude'a 11 istek verdin — geçen haftadan daha yoğun bir tempo.",
    "Portfolyo Yenileme kilidi 14 saattir açık; kimse tutmuyor olabilir, kontrol etmekte fayda var.",
    "3/3 aktif proje sınırı dolu; yeni bir fikir gelirse önce birini bitirmen ya da beklemeye alman gerekecek.",
    "Kişisel Blog'un bırakma tarihine 37 gün var: 10 yazı gerekiyor, şu an 3.",
  ],
  deepAnalysis: { found: true, date: "2026-09-23", path: "70 Günlük/Analiz/2026-09-23.md", lines: [
    "Bugün Hava Durumu Uygulaması'nın API anahtarı test edildi.",
    "Kişisel Blog için yeni yazı taslağı hazırlandı; yorum bölümü sıradaki büyük adım.",
    "Portfolyo Yenileme'de proje kartları bölümü yarım; hafta sonu bloğu kritik.",
    "Codex'in yeni kancalara güven vermesi gerekiyor; bu elle yapılan tek adım.",
  ] },
  income: { found: false, monthTl: 0, monthUsd: 0, targetUsd: 100, pct: 0, rate: 41 },
  usage: USAGE,
  stats: { notes: 58, files: 64, folders: 21, links: 132, broken: 1, words: 9400, indexedAt: iso(TODAY, "00:41:00"), indexMs: 120 },
};

// ---------------- Tree (curated, §5.10) ----------------
export const TREE = [
  { id: "g:baslangic", label: "Başlangıç", kind: "group", icon: "home", noteKind: null, badge: null, health: null, count: 5, target: null, children: [
    { id: "n:01 Şimdi.md", label: "01 Şimdi", kind: "note", icon: "doc", noteKind: "simdi", badge: null, health: null, count: null, target: { type: "note", path: "01 Şimdi.md", anchor: null }, children: [] },
    { id: "n:00 Başla Buradan.md", label: "00 Başla Buradan", kind: "note", icon: "doc", noteKind: "sistem", badge: null, health: null, count: null, target: { type: "note", path: "00 Başla Buradan.md", anchor: null }, children: [] },
    { id: "n:04 İstek Defteri.md", label: "04 İstek Defteri", kind: "note", icon: "doc", noteKind: "sistem", badge: null, health: null, count: null, target: { type: "note", path: "04 İstek Defteri.md", anchor: null }, children: [] },
    { id: "n:AGENTS.md", label: "AGENTS", kind: "note", icon: "doc", noteKind: "sistem", badge: null, health: null, count: null, target: { type: "note", path: "AGENTS.md", anchor: null }, children: [] },
    { id: "n:CLAUDE.md", label: "CLAUDE", kind: "note", icon: "doc", noteKind: "sistem", badge: null, health: null, count: null, target: { type: "note", path: "CLAUDE.md", anchor: null }, children: [] },
  ] },
  { id: "g:projeler", label: "Projeler", kind: "group", icon: "project", noteKind: null, badge: null, health: null, count: PROJECTS.length, target: null, children: PROJECTS.map(p => ({
    id: `p:${p.name}`, label: p.name, kind: "project", icon: "project", noteKind: null,
    badge: { text: p.durum, tone: p.durum === "aktif" ? "teal" : "grey" }, health: p.health.level, count: null,
    target: { type: "project", name: p.name, section: "genel" },
    children: [
      { id: `n:${p.path}`, label: "Kart", kind: "note", icon: "doc", noteKind: "proje-karti", badge: null, health: null, count: null, target: { type: "note", path: p.path, anchor: null }, children: [] },
      { id: `k:${p.folder}/Kayıt.md`, label: "Kayıt", kind: "kayit", icon: "kayit", noteKind: "kayit", badge: null, health: null, count: 3, target: { type: "note", path: `20 Projeler/${p.folder}/Kayıt.md`, anchor: null }, children: [] },
      { id: `g:${p.folder}:gorevler`, label: "Görevler (2)", kind: "group", icon: "task", noteKind: null, badge: null, health: null, count: 2, target: { type: "project", name: p.name, section: "gorevler" }, children: [] },
      { id: `g:${p.folder}:acik`, label: "Açık kalanlar (1)", kind: "group", icon: "warning", noteKind: null, badge: null, health: null, count: 1, target: { type: "project", name: p.name, section: "acik" }, children: [] },
    ],
  })) },
  { id: "g:alanlar", label: "Alanlar", kind: "group", icon: "area", noteKind: null, badge: null, health: null, count: 2, target: null, children: [
    { id: "n:40 Alanlar/İş Başvuruları.md", label: "İş Başvuruları", kind: "note", icon: "doc", noteKind: "alan", badge: null, health: null, count: null, target: { type: "note", path: "40 Alanlar/İş Başvuruları.md", anchor: null }, children: [] },
    { id: "n:40 Alanlar/Finans Takip.md", label: "Finans Takip", kind: "note", icon: "doc", noteKind: "alan", badge: null, health: null, count: null, target: { type: "note", path: "40 Alanlar/Finans Takip.md", anchor: null }, children: [] },
  ] },
  { id: "g:park", label: "Park Yeri", kind: "group", icon: "idea", noteKind: null, badge: null, health: null, count: 2, target: null, children: [
    { id: "n:30 Park Yeri/Tarif defteri uygulaması.md", label: "Tarif defteri uygulaması", kind: "note", icon: "doc", noteKind: "fikir", badge: { text: "9 gün", tone: "yellow" }, health: null, count: null, target: { type: "note", path: "30 Park Yeri/Tarif defteri uygulaması.md", anchor: null }, children: [] },
    { id: "n:30 Park Yeri/Haftalık bülten.md", label: "Haftalık bülten", kind: "note", icon: "doc", noteKind: "fikir", badge: null, health: null, count: null, target: { type: "note", path: "30 Park Yeri/Haftalık bülten.md", anchor: null }, children: [] },
  ] },
  { id: "g:gelen", label: "Gelen Kutusu", kind: "group", icon: "inbox", noteKind: null, badge: { text: "1", tone: "yellow" }, health: null, count: 1, target: null, children: [
    { id: "n:10 Gelen Kutusu/Yorum bildirimleri fikri.md", label: "Yorum bildirimleri fikri", kind: "note", icon: "doc", noteKind: "gelen", badge: null, health: null, count: null, target: { type: "note", path: "10 Gelen Kutusu/Yorum bildirimleri fikri.md", anchor: null }, children: [] },
  ] },
  { id: "g:kaynaklar", label: "Kaynaklar", kind: "group", icon: "research", noteKind: null, badge: null, health: null, count: 3, target: null, children: [
    { id: "n:50 Kaynaklar/Renk Paletleri.md", label: "Renk Paletleri", kind: "note", icon: "doc", noteKind: "arastirma", badge: null, health: null, count: null, target: { type: "note", path: "50 Kaynaklar/Renk Paletleri.md", anchor: null }, children: [] },
    { id: "n:50 Kaynaklar/Kitap Notları.md", label: "Kitap Notları", kind: "note", icon: "doc", noteKind: "ham", badge: null, health: null, count: null, target: { type: "note", path: "50 Kaynaklar/Kitap Notları.md", anchor: null }, children: [] },
    { id: "n:50 Kaynaklar/Faydalı Bağlantılar.md", label: "Faydalı Bağlantılar", kind: "note", icon: "doc", noteKind: "kaynak", badge: null, health: null, count: null, target: { type: "note", path: "50 Kaynaklar/Faydalı Bağlantılar.md", anchor: null }, children: [] },
  ] },
  { id: "g:ekip", label: "Ekip", kind: "group", icon: "role", noteKind: null, badge: null, health: null, count: ROLES.length, target: null, children: [
    { id: "g:ekip:roller", label: `Roller (${ROLES.length})`, kind: "group", icon: "role", noteKind: null, badge: null, health: null, count: ROLES.length, target: null,
      children: DEPARTMENTS.map(dep => ({
        id: `g:dep:${dep}`, label: dep, kind: "group", icon: "role", noteKind: null, badge: null, health: null,
        count: ROLES.filter(r => r.departman === dep).length, target: null,
        children: ROLES.filter(r => r.departman === dep).map(r => ({
          id: `n:${r.path}`, label: r.title, kind: "role", icon: "role", noteKind: "rol",
          badge: { text: r.arac.includes("Codex") ? "Codex" : "Claude", tone: "grey" }, health: null, count: null,
          target: { type: "note", path: r.path, anchor: null }, children: [],
        })),
      })) },
    { id: "n:60 Ekip/Şirket Şeması.md", label: "Şirket Şeması", kind: "note", icon: "doc", noteKind: "ekip", badge: null, health: null, count: null, target: { type: "note", path: "60 Ekip/Şirket Şeması.md", anchor: null }, children: [] },
  ] },
  { id: "g:gunluk", label: "Günlük", kind: "group", icon: "journal", noteKind: null, badge: null, health: null, count: 1, target: null, children: [
    { id: "n:70 Günlük/Analiz/2026-09-23.md", label: "Analiz — 23 Eylül", kind: "note", icon: "doc", noteKind: "gunluk", badge: null, health: null, count: null, target: { type: "note", path: "70 Günlük/Analiz/2026-09-23.md", anchor: null }, children: [] },
  ] },
  { id: "g:istek", label: "İstek defteri", kind: "group", icon: "ledger", noteKind: null, badge: null, health: null, count: 5, target: null, children: [0,1,2,3,4].map(i => {
    const day = addDays(TODAY, -i);
    return { id: `l:${day}`, label: `${trLabel(day)} · ${5 - i} istek`, kind: "ledgerDay", icon: "ledger", noteKind: null, badge: null, health: null, count: 5 - i, target: { type: "ledger", day }, children: [] };
  }) },
  { id: "g:sistem", label: "Sistem", kind: "group", icon: "system", noteKind: null, badge: null, health: null, count: 2, target: null, children: [
    { id: "n:_sistem/istek-defteri/istekler.jsonl", label: "istekler.jsonl", kind: "file", icon: "code", noteKind: null, badge: null, health: null, count: null, target: null, children: [] },
    { id: "n:_sistem/guvenlik/bekleyenler.jsonl", label: "bekleyenler.jsonl", kind: "file", icon: "code", noteKind: null, badge: null, health: null, count: null, target: null, children: [] },
  ] },
  { id: "g:arsiv", label: "Arşiv", kind: "group", icon: "archive", noteKind: null, badge: null, health: null, count: 0, target: null, children: [] },
  { id: "g:tum", label: "Tüm dosyalar", kind: "group", icon: "raw", noteKind: null, badge: null, health: null, count: 64, target: null, children: [] },
];

// ---------------- Notes (3 rendered) ----------------
export const NOTES = {
  "20 Projeler/Portfolyo Yenileme/Portfolyo Yenileme.md": {
    path: "20 Projeler/Portfolyo Yenileme/Portfolyo Yenileme.md", title: "Portfolyo Yenileme", kind: "proje-karti", project: "Portfolyo Yenileme",
    breadcrumb: ["Projeler", "Portfolyo Yenileme"],
    html: `<h1 id="h-15">Portfolyo Yenileme</h1><h2 id="h-16">Amaç</h2><p>Eski portfolyo sitesini güncel projelerle yenilemek: ana sayfa, proje kartları ve iletişim formu tek seferde elden geçsin.</p><h2 id="h-20">Bitti Tanımı</h2><ul><li class="task"><span class="cb done"></span>Ana sayfa yenilendi</li><li class="task"><span class="cb"></span>Proje kartları bölümü</li><li class="task"><span class="cb"></span>İletişim formu</li></ul><div class="callout callout-warning"><div class="callout-title">Engel</div>Sürekli yeni tasarım denemek isteği. Yeni deneme değil, <strong>bitirmek</strong> bu projenin başarısı.</div><p>Bağlantı: <a class="wl" data-note="20 Projeler/Portfolyo Yenileme/Kayıt.md">Kayıt</a> · Eski not: <span class="wl broken" title="Not bulunamadı: Eski Tasarım Notu">Eski Tasarım Notu</span></p>`,
    frontmatter: [{ key: "durum", value: "aktif" }, { key: "odak", value: "false" }, { key: "kilit", value: "claude 2026-09-24 00:00" }],
    headings: [{ level: 1, text: "Portfolyo Yenileme", anchor: "h-15" }, { level: 2, text: "Amaç", anchor: "h-16" }, { level: 2, text: "Bitti Tanımı", anchor: "h-20" }],
    outLinks: [{ label: "Kayıt", path: "20 Projeler/Portfolyo Yenileme/Kayıt.md", broken: false }, { label: "Eski Tasarım Notu", path: null, broken: true }],
    backLinks: [{ path: "01 Şimdi.md", title: "01 Şimdi", kind: "simdi", context: "Aktif projeler tablosunda geçiyor" }],
    problems: PROBLEMS.filter(p => p.project === "Portfolyo Yenileme"), tasks: { open: 1, done: 1 }, words: 420, size: 2400,
    modified: iso("2026-09-23", "23:14:00"), isMarkdown: true, truncated: false, role: null,
  },
  "20 Projeler/Portfolyo Yenileme/Kayıt.md": {
    path: "20 Projeler/Portfolyo Yenileme/Kayıt.md", title: "Kayıt", kind: "kayit", project: "Portfolyo Yenileme",
    breadcrumb: ["Projeler", "Portfolyo Yenileme", "Kayıt"],
    html: `<h1 id="h-1">Kayıt</h1><h3 id="h-3">2026-09-23 23:14 — codex — devir notu</h3><p><strong>Yapılan:</strong> Ana sayfa yayına alındı, eski görseller sıkıştırıldı.</p><p><strong>Doğrulama:</strong> Sayfa üç tarayıcıda açılıp kontrol edildi.</p><p><strong>Açık kalan:</strong> Proje kartları için tasarım bekleniyor.</p><h3 id="h-8">KARAR 2026-09-24 — proje kartları tek bileşen olacak</h3><p><strong>Karar:</strong> Kartlar tek bir yeniden kullanılabilir bileşen olarak yazılacak.</p><p><strong>Neden:</strong> Yeni proje eklemek tek satır veri eklemek kadar kolay olsun.</p>`,
    frontmatter: [], headings: [{ level: 1, text: "Kayıt", anchor: "h-1" }, { level: 3, text: "2026-09-23 23:14 — codex — devir notu", anchor: "h-3" }, { level: 3, text: "KARAR 2026-09-24 — proje kartları", anchor: "h-8" }],
    outLinks: [], backLinks: [{ path: "20 Projeler/Portfolyo Yenileme/Portfolyo Yenileme.md", title: "Portfolyo Yenileme", kind: "proje-karti", context: "Son Devir bağlantısı" }],
    problems: [], tasks: { open: 0, done: 0 }, words: 120, size: 900, modified: iso("2026-09-23", "23:14:00"), isMarkdown: true, truncated: false, role: null,
  },
  "AGENTS.md": {
    path: "AGENTS.md", title: "AGENTS", kind: "sistem", project: null, breadcrumb: ["Sistem", "AGENTS"],
    html: `<h1 id="h-1">AGENTS</h1><p>Bu kasadaki tüm yapay zekâlar için tek kural dosyası.</p><table><thead><tr><th class="al-l">Kural</th><th class="al-l">Açıklama</th></tr></thead><tbody><tr><td>Kilit</td><td>Yazmadan önce <code>kilit:</code> alanını kontrol et.</td></tr><tr><td>Devir</td><td>Çalışma bitince <span class="tag">#devir</span> notu yaz.</td></tr></tbody></table><blockquote>Gerçek veri: kasaya asla test verisi yazma.</blockquote>`,
    frontmatter: [], headings: [{ level: 1, text: "AGENTS", anchor: "h-1" }],
    outLinks: [], backLinks: [], problems: [], tasks: { open: 0, done: 0 }, words: 90, size: 600,
    modified: iso("2026-09-20", "10:00:00"), isMarkdown: true, truncated: false, role: null,
  },
};

// ---------------- Graph (~50 nodes) ----------------
function buildGraph() {
  const nodes = []; const links = [];
  const cats = ["proje-karti","kayit","gorev","rol","fikir","not","eksik"];
  const catIndex = (k) => cats.indexOf(k);
  PROJECTS.forEach((p, pi) => {
    nodes.push({ id: `p:${p.name}`, name: p.name, kind: "proje-karti", category: catIndex("proje-karti"), degree: 4, project: p.name, missing: false, modified: iso(p.sonGuncelleme || TODAY) });
    nodes.push({ id: `k:${p.name}`, name: `${p.name} · Kayıt`, kind: "kayit", category: catIndex("kayit"), degree: 2, project: p.name, missing: false, modified: iso(TODAY) });
    links.push({ source: `p:${p.name}`, target: `k:${p.name}`, count: 3 });
    for (let g = 0; g < 2; g++) {
      const id = `g:${p.name}:${g}`;
      nodes.push({ id, name: `${p.name} görev ${g + 1}`, kind: "gorev", category: catIndex("gorev"), degree: 1, project: p.name, missing: false, modified: iso(TODAY) });
      links.push({ source: `p:${p.name}`, target: id, count: 1 });
    }
  });
  ROLES.slice(0, 12).forEach(r => {
    nodes.push({ id: `r:${r.title}`, name: r.title, kind: "rol", category: catIndex("rol"), degree: 1, project: null, missing: false, modified: iso(TODAY) });
    links.push({ source: "p:Kişisel Blog", target: `r:${r.title}`, count: 1 });
  });
  ["Tarif defteri uygulaması", "Haftalık bülten"].forEach(f => {
    nodes.push({ id: `f:${f}`, name: f, kind: "fikir", category: catIndex("fikir"), degree: 1, project: null, missing: false, modified: iso(TODAY) });
  });
  for (let i = 0; i < 20; i++) {
    nodes.push({ id: `nt:${i}`, name: `Not ${i + 1}`, kind: "not", category: catIndex("not"), degree: 1, project: null, missing: false, modified: iso(addDays(TODAY, -i)) });
    links.push({ source: `p:${PROJECTS[i % PROJECTS.length].name}`, target: `nt:${i}`, count: 1 });
  }
  nodes.push({ id: "eksik:Eski Tasarım Notu", name: "Eski Tasarım Notu", kind: "eksik", category: catIndex("eksik"), degree: 1, project: "Portfolyo Yenileme", missing: true, modified: null });
  links.push({ source: "p:Portfolyo Yenileme", target: "eksik:Eski Tasarım Notu", count: 1 });
  return {
    nodes, links,
    categories: cats.map(k => ({ name: { "proje-karti": "Proje kartı", kayit: "Kayıt", gorev: "Görev", rol: "Rol", fikir: "Fikir", not: "Not", eksik: "Eksik not" }[k], kind: k })),
    stats: { nodes: nodes.length, links: links.length, missing: 1, truncated: false },
  };
}
export const GRAPH = buildGraph();

// ---------------- Company (Şirketim extras) ----------------
export const COMPANY_EXTRA = {
  locks: [{ project: "Portfolyo Yenileme", holder: "claude", since: iso("2026-09-24", "00:00:00"), hours: 14, stale: true }],
  activeTasks: [
    { project: "Kişisel Blog", kimlik: "G-002", title: "Spam filtresi araştırması", durum: "kontrol", durumText: "Kontrol", atanan: "codex", sonTarih: null, path: "20 Projeler/Kişisel Blog/Görevler/G-002 Spam filtresi araştırması.md", modified: iso("2026-09-21","10:00:00"), ageDays: 3 },
    { project: "Portfolyo Yenileme", kimlik: "G-002", title: "Proje kartları bileşeni", durum: "verildi", durumText: "Verildi", atanan: "codex", sonTarih: null, path: "20 Projeler/Portfolyo Yenileme/Görevler/G-002 Proje kartları bileşeni.md", modified: iso("2026-09-24","00:05:00"), ageDays: 0 },
  ],
  scheduled: TOOLS.scheduledTasks,
  claudeSessions: [
    { sessionId: "sess-a1b2", title: "Portfolyo proje kartları", projectDir: "Projeler\\portfolyo", lastActivity: iso("2026-09-24","00:27:41"), state: "aktif", appJob: true },
    { sessionId: "sess-c3d4", title: "Yorum formu testleri", projectDir: "ornek-kasa", lastActivity: iso("2026-09-23","21:40:19"), state: "izin", appJob: false },
  ],
  tamGaz: { on: false, start: null, end: null, hours: 0 },
  watch: { running: false, task: "codex-nobet", waitingQuota: false, lastResult: "Son çalışma: 20:11'de bitti (çıkış 0)" },
  tamGazLog: ["21:14 · Tam Gaz kapalı, çalışılmadı.", "20:14 · Tam Gaz kapalı, çalışılmadı."],
};

// Kasa yolu uydurmadır; gerçek uygulamada Ayarlar > Kasa'dan seçilir.
export const SETTINGS = {
  vaultPath: "C:\\Kasalar\\ornek-kasa", vaultOk: true, uiScale: 100, lockMinutes: 10, usdRate: 41,
  theme: "light", animations: true, keepAwake: true, dataDir: "%APPDATA%\\Ordinaryunus", version: IMZA.surum, dryRun: false,
};
