#!/usr/bin/env node
// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
// İş nöbetçisi: bir işi Claude'a ya da Codex'e verir. Kullanım hakkı (kota) dolarsa sıfırlanma saatine kadar bekler,
// sonra AYNI oturumu kaldığı yerden devam ettirir (Claude: --resume, Codex: exec resume). İş bitene, hata çıkana ya da
// deneme hakkı bitene kadar sürer. Ordinaryunus'un "Claude'a ver" / "Codex'e ver" düğmeleri ve Tam Gaz işçisi bunu
// ayrı bir süreç olarak başlatır; bu yüzden uygulama kapansa da iş devam eder.
//
//   node _sistem/araclar/is-nobetcisi.mjs --arac claude|codex --is <damga> (--istem-dosyasi <yol> | --gorev "<kasaya göre paket yolu>")
//        [--ekdizin <klasör>] [--cd <kasa dışı çalışma klasörü, sadece codex>] [--klasor <iş klasörü>]
//        [--bekle 20] [--deneme 12] [--pay-sn 120] [--sure-dk 120] [--web] [--exe <claude.exe>] [--sahte <deneme betiği>]
//   --web: Claude işi internetten sayfa da okuyabilir (WebFetch). Varsayılan kapalı; arama (WebSearch) hep açık.
//
// Dosyalar (iş klasörü, varsayılan %APPDATA%\Ordinaryunus\claude-isler\ ya da ...\codex-isler\):
//   <damga>.jsonl        aracın her çıktı satırı olduğu gibi + {"type":"ordinaryunus","event":…,"kaynak":"nobetci"} satırları
//   <damga>.nobet.json   nöbetçinin durumu: calisiyor | kota-bekliyor | bitti | hata | giris | durduruldu
//                        (+ bekleUntil, sessionId, deneme, adimVar; "guncel" alanı dakikada bir tazelenir = nöbetçi canlı)
//   <damga>.kilit.<n>    sahiplik kilidi, NESİLLİ: her yeni sahip bir sonraki nesli "yoksa oluştur" ile alır (atomik; iki
//                        süreç aynı nesli alamaz). İçerik {pid, t, cocukPid}; t dakikada bir tazelenir. Hiçbir kilit silinmez.
//   <damga>.iptal        uygulama bunu oluşturursa nöbetçi aracı durdurur ve çıkar
//   <damga>.simdi        kota beklerken uygulama bu dosyaya YENİ bir değer yazarsa ("Şimdi dene") bekleme biter, yeniden denenir
// Çıkış kodu: 0 bitti, 1 hata, 2 yanlış kullanım ya da iş zaten çalışıyor, 3 durduruldu, 4 giriş gerekli, 5 kota deneme hakkı bitti.
// İstem araca komut satırından DEĞİL standart girişten (stdin) verilir: süreç listesinde görünmez, uzunluk sınırına takılmaz.
// Güvenlik: izin atlama bayrakları ASLA yok; Codex hep -s workspace-write; kancalar (güvenlik kapısı, istek defteri) çalışır.
// Dayanıklılık kuralları: sınıflandırma son terminal olaya bakar,
// kayıt/girdi hataları başarıya dönüşmez, öldürme başarısızsa tekrar denenir ve canlı araç varken kilit bırakılmaz,
// oturum kimliği olmayan yarım iş baştan başlatılmaz.
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import readline from 'node:readline'
import { spawn, spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

const KASA = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..')
const arg = (ad, varsayilan) => { const i = process.argv.indexOf(ad); return i > 0 ? process.argv[i + 1] : varsayilan }
const ARAC = arg('--arac', 'claude')
const IS = arg('--is')
const KLASOR = arg('--klasor', path.join(process.env.APPDATA || '', 'Ordinaryunus', ARAC === 'codex' ? 'codex-isler' : 'claude-isler'))
const BEKLE_DK = Number(arg('--bekle', 20)), DENEME = Number(arg('--deneme', 12)), PAY_SN = Number(arg('--pay-sn', 120))
const SURE_DK = Number(arg('--sure-dk', 120))
const EKDIZIN = arg('--ekdizin'), CD = arg('--cd'), SAHTE = arg('--sahte'), GOREV = arg('--gorev'), ISTEM_DOSYASI = arg('--istem-dosyasi')
const ISTEM_SINIRI = 4000
const KALP_MS = 60_000, BAYAT_MS = 5 * 60_000          // kalp atışı ve "sahibi ölmüş" sayılma süresi
const OLDURME_ARALIK_MS = 5_000, OLDURME_PES_MS = 90_000 // durdurulamayan araç: 5 sn'de bir yeniden dene, 90 sn sonra pes et

// Doğrudan mı çalıştırıldı, yoksa bir test mi içeri aldı? İçeri alınınca hiçbir yan etki olmaz.
const DOGRUDAN = !!process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)
function kullanimHatasi(metin) { console.error(metin); process.exit(2) }
if (DOGRUDAN) {
  if (!['claude', 'codex'].includes(ARAC)) kullanimHatasi('--arac claude ya da codex olmalı')
  if (!IS || !/^[0-9A-Za-z-]{6,60}$/.test(IS) || (!ISTEM_DOSYASI && !GOREV))
    kullanimHatasi('kullanım: --arac claude|codex --is <damga> (--istem-dosyasi <yol> | --gorev "<paket yolu>") [--ekdizin <klasör>] [--cd <klasör>]')
  if (GOREV && !fs.existsSync(path.join(KASA, GOREV))) kullanimHatasi(`görev paketi yok: ${GOREV}`)
  if (CD && !fs.existsSync(CD)) kullanimHatasi(`çalışma klasörü yok: ${CD}`)
  // Sayısal seçenekler: saçma değerler sessizce "kota bitti" gibi yanlış sonuçlara yol açmasın
  if (!Number.isInteger(DENEME) || DENEME < 1 || DENEME > 100) kullanimHatasi('--deneme 1-100 arası tam sayı olmalı')
  if (!Number.isFinite(BEKLE_DK) || BEKLE_DK <= 0 || BEKLE_DK > 24 * 60) kullanimHatasi('--bekle 0-1440 dakika arası olmalı')
  if (!Number.isFinite(PAY_SN) || PAY_SN < 0 || PAY_SN > 3600) kullanimHatasi('--pay-sn 0-3600 arası olmalı')
  if (!Number.isFinite(SURE_DK) || SURE_DK <= 0 || SURE_DK > 24 * 60) kullanimHatasi('--sure-dk 0-1440 arası olmalı')
}

// Claude'a izin verilen araçlar. Kabuk (Bash) izni YOK: "node _sistem/araclar/*", "dotnet build", "git diff --output" gibi
// kurallar güvenlik kapısını atlatan yan yollar açıyordu. İnternetten sayfa okuma (WebFetch)
// gizli talimatla kasa içeriğini dışarı taşıyabileceği için sadece --web verilince açılır.
const WEB = process.argv.includes('--web')
const CLAUDE_IZINLI = 'Read,Grep,Glob,Edit,Write,TodoWrite,WebSearch' + (WEB ? ',WebFetch' : '')
const YASAK_BAYRAK = /dangerously|bypassPermissions|--bare|--safe-mode|danger-full-access|--full-auto|--yolo/i
const GIRIS = /oauth token has expired|oauth session expired|authentication_error|invalid api key|please run \/login|run \/login|not logged in|codex login|unauthori[sz]ed/i
const KOTA = /usage limit|limit reached|rate_limit_error|rate limit|5-hour limit|weekly limit|hit your limit|try again at|\b429\b/i
// Codex'te "iş adımı" sayılan öğeler: bunlardan biri olduysa iş gerçekten başlamıştır
const CODEX_ADIM = /^(command_execution|file_change|mcp_tool_call|web_search|patch_apply)$/

const JSONL = path.join(KLASOR, `${IS}.jsonl`), NOBET = path.join(KLASOR, `${IS}.nobet.json`)
const KILIT_ON = `${IS}.kilit.`, IPTAL = path.join(KLASOR, `${IS}.iptal`), SIMDI = path.join(KLASOR, `${IS}.simdi`)
const DURDUR = path.join(KASA, '_sistem', 'guvenlik', 'DURDUR')

const simdi = () => new Date().toISOString()
const kisaBekle = ms => Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms)
// Kayıt yazımı ASLA fırlatmaz: hata olursa bayrak kalkar, çalışan deneme bunu görüp denetimli durdurur
let kayitHatasi = null
const olay = (event, ek = {}) => {
  try { fs.appendFileSync(JSONL, JSON.stringify({ type: 'ordinaryunus', event, t: simdi(), kaynak: 'nobetci', arac: ARAC, ...ek }) + '\n') }
  catch (e) { kayitHatasi ||= e.message }
}
let durum = { is: IS, arac: ARAC, pid: process.pid, durum: 'calisiyor', deneme: 0, sessionId: null, adimVar: false, bekleUntil: null, exitCode: null, basla: simdi(), guncel: simdi() }
// Atomik yazım: sürece özel geçici dosya + yeniden adlandırma. Hata olursa kayıt hatası bayrağı kalkar.
const atomikYaz = (hedef, veri) => { const g = `${hedef}.${process.pid}.tmp`; fs.writeFileSync(g, veri); fs.renameSync(g, hedef) }
const durumYaz = ek => {
  durum = { ...durum, ...ek, guncel: simdi() }
  try { atomikYaz(NOBET, JSON.stringify(durum, null, 1)) } catch (e) { kayitHatasi ||= e.message }
}
const durmali = () => fs.existsSync(IPTAL) || fs.existsSync(DURDUR)
const yasiyor = pid => { if (!pid) return false; try { process.kill(pid, 0); return true } catch (e) { return e.code === 'EPERM' } }
const sinyalOku = () => { try { return fs.readFileSync(SIMDI, 'utf8') } catch { return null } }

// ---------------------------------------------------------------- nesilli sahiplik kilidi
let kilitNesli = 0, kilitIcerik = null
const kilitYolu = n => path.join(KLASOR, KILIT_ON + n)
function kilitOku(n) {
  // Kilitler tam içerikle doğar (aşağıda "bağlantı ile oluşturma"); yine de disk gecikmesine karşı kısa tekrar
  for (let i = 0; i < 5; i++) {
    try { const k = JSON.parse(fs.readFileSync(kilitYolu(n), 'utf8')); if (k && typeof k === 'object') return k } catch {}
    kisaBekle(40)
  }
  return null
}
// Bu nöbetçinin başlangıç anı ve başka bir sürecin başlangıç anı (Windows pid numaralarını yeniden kullanır:
// "pid yaşıyor" tek başına yetmez, aynı süreç mi diye başlangıç anına da bakılır)
const BENIM_BASLANGIC = new Date(Date.now() - process.uptime() * 1000).toISOString()
function surecBaslangici(pid) {
  if (process.platform !== 'win32') return null
  try {
    const r = spawnSync('powershell', ['-NoProfile', '-Command', `(Get-Process -Id ${Number(pid)} -ErrorAction Stop).StartTime.ToUniversalTime().ToString('o')`],
      { encoding: 'utf8', windowsHide: true, timeout: 8000 })
    const t = Date.parse(String(r.stdout || '').trim())
    return Number.isFinite(t) ? t : null
  } catch { return null }
}
// Sahip canlı mı? Kalp atışının yaşı ölüm kanıtı DEĞİLDİR (süreç duraklamış olabilir).
// Canlı sayılır: durdurulamamış araç (cocukPid) yaşıyorsa; ya da sahibin pid'i yaşıyor ve başlangıç anı kilitteki ile
// aynıysa (±5 sn). Başlangıç anı öğrenilemiyorsa güvenli taraf: pid yaşıyorsa canlı.
export function sahipCanli(k, { yasiyorMu = yasiyor, baslangicOku = surecBaslangici, benimPid = process.pid } = {}) {
  if (!k) return false
  const ayniSurec = (pid, bas) => {
    if (!pid || !yasiyorMu(pid)) return false
    const gercek = bas ? baslangicOku(pid) : null
    return gercek == null || !bas || Math.abs(gercek - Date.parse(bas)) < 5000
  }
  if (k.cocukPid && ayniSurec(k.cocukPid, k.cocukBas)) return true
  return !!k.pid && k.pid !== benimPid && ayniSurec(k.pid, k.bas)
}
const enYuksekNesil = () => {
  const nesiller = fs.readdirSync(KLASOR).filter(f => f.startsWith(KILIT_ON))
    .map(f => Number(f.slice(KILIT_ON.length))).filter(n => Number.isInteger(n) && n > 0)
  return nesiller.length ? Math.max(...nesiller) : 0
}
// Kilidi TAM İÇERİKLE atomik oluşturur: önce sürece özel geçici dosyaya yazılır, sonra sabit bağlantı (hard link) ile
// kilit adına bağlanır. Hedef varsa bağlantı EEXIST verir; böylece hiçbir okuyucu yarı yazılmış (boş) kilit görmez.
function kilitDosyasiOlustur(hedef, icerik) {
  const gecici = path.join(KLASOR, `${IS}.kilit-yeni.${process.pid}.tmp`)
  fs.writeFileSync(gecici, icerik)
  try { fs.linkSync(gecici, hedef) }
  catch (e) {
    // Sabit bağlantı desteklenmiyorsa (FAT gibi) eski yola düş: "yoksa oluştur" ile yaz
    if (e.code === 'EPERM' || e.code === 'ENOTSUP' || e.code === 'EXDEV') fs.writeFileSync(hedef, icerik, { flag: 'wx' })
    else throw e
  } finally { try { fs.unlinkSync(gecici) } catch {} } // kendi geçici dosyamız
}
function kilitAl() {
  for (let tur = 0; tur < 50; tur++) {
    const n = enYuksekNesil()
    if (n) {
      const k = kilitOku(n)
      // Okunamayan (bozuk) kilit belirsizdir: otomatik devralınmaz, insan kararı gerekir
      if (!k) return { mesgul: `kilit kaydı okunamadı (${KILIT_ON}${n}); güvenlik için başlatılmadı` }
      if (sahipCanli(k)) return { mesgul: k.cocukPid && yasiyor(k.cocukPid) && !(k.pid && yasiyor(k.pid)) ? `durdurulamamış araç hâlâ çalışıyor (pid ${k.cocukPid})` : `pid ${k.pid}` }
    }
    kilitIcerik = { pid: process.pid, bas: BENIM_BASLANGIC, t: simdi(), cocukPid: null, cocukBas: null }
    try {
      // Aynı nesli ancak BİR süreç alabilir; kaybeden döngüde yeni sahibi (tam içerikli kaydı) görür
      kilitDosyasiOlustur(kilitYolu(n + 1), JSON.stringify(kilitIcerik))
      kilitNesli = n + 1
      // Sigorta: bizden yüksek bir nesil varsa sahip biz değiliz
      if (enYuksekNesil() !== kilitNesli) { kilitNesli = 0; return { mesgul: 'başka bir nöbetçi daha yeni kilit aldı' } }
      return { mesgul: null, oncekiVar: n > 0 }
    } catch (e) { if (e.code !== 'EEXIST') throw e }
  }
  return { mesgul: 'kilit alınamadı (çok fazla yarış)' }
}
function kilitGuncelle(ek = {}) {
  if (!kilitNesli) return
  kilitIcerik = { ...kilitIcerik, ...ek, t: simdi() }
  try { atomikYaz(kilitYolu(kilitNesli), JSON.stringify(kilitIcerik)) } catch {} // başarısızsa bir sonraki kalp atışında denenir
}
// Bırakma: silmek yok. Araç durdurulamadıysa cocukPid kilitte kalır → o araç yaşadıkça kimse bu işi yeniden başlatamaz.
function kilitBirak(cocukCanliPid = null) {
  if (!kilitNesli) return
  kilitIcerik = { pid: 0, t: simdi(), cocukPid: cocukCanliPid, bitis: simdi() }
  try { atomikYaz(kilitYolu(kilitNesli), JSON.stringify(kilitIcerik)) } catch {}
  kilitNesli = 0
}

// Claude komut satırını arar; ilk bulunan kazanır:
//   1. --exe ile verilen yol,
//   2. Claude masaüstü uygulamasının kurduğu en yeni %APPDATA%\Claude\claude-code\<sürüm>\claude.exe (sürümler sayı
//      olarak karşılaştırılır, yazı olarak değil),
//   3. yerel kurucunun koyduğu %USERPROFILE%\.local\bin\claude.exe,
//   4. PATH'teki ilk claude.exe.
// Sadece .exe kabul edilir: .cmd ve .ps1 sarmalayıcıları kabuk olmadan başlatılamaz (uygulamadaki ProcessRunner kuralı).
// Uygulama (Jobs/ClaudeCli.cs, Find) aynı sırayla arar; biri değişirse öbürü de değişmeli.
const dosyaMi = p => { try { return fs.statSync(p).isFile() } catch { return false } }
export function claudeBul(ortam = process.env, ev = os.homedir()) {
  if (arg('--exe')) return arg('--exe')
  const kok = path.join(ortam.APPDATA || '', 'Claude', 'claude-code')
  let surumler = []
  try { surumler = fs.existsSync(kok) ? fs.readdirSync(kok) : [] } catch {}
  surumler = surumler.filter(d => /^\d+(\.\d+)*$/.test(d) && fs.existsSync(path.join(kok, d, 'claude.exe')))
    .sort((a, b) => { const x = a.split('.').map(Number), y = b.split('.').map(Number)
      for (let i = 0; i < Math.max(x.length, y.length); i++) if ((x[i] || 0) !== (y[i] || 0)) return (x[i] || 0) - (y[i] || 0)
      return 0 })
  if (surumler.length) return path.join(kok, surumler.at(-1), 'claude.exe')
  const yerel = path.join(ev, '.local', 'bin', 'claude.exe')
  if (dosyaMi(yerel)) return yerel
  for (const parca of String(ortam.PATH ?? ortam.Path ?? '').split(path.delimiter)) {
    const klasor = parca.trim().replace(/^"(.*)"$/, '$1')
    if (!klasor) continue
    const aday = path.join(klasor, 'claude.exe')
    if (path.isAbsolute(aday) && dosyaMi(aday)) return aday
  }
  return null
}

// Kota mesajından sıfırlanma zamanını çıkarır. Bilinen biçimler:
//   Claude: "…|1759201200" (saniye), "resets 3am", "resets at 3:09 PM"   Codex: "try again at 3:09 AM", "try again in 12 minutes"
// Güvenli değilse null döner (o zaman varsayılan bekleme kullanılır): geçersiz saat, ya da mesajdaki saat dilimi
// bilgisayarınkinden farklıysa (yanlış saatte uyanmaktansa düzenli aralıkla denemek daha doğru).
export function sifirlanmaZamani(metin, suan = new Date(), sistemBolgesi = Intl.DateTimeFormat().resolvedOptions().timeZone) {
  const s = String(metin || '')
  const epoch = s.match(/\|(\d{10})\b/)
  if (epoch) return new Date(Number(epoch[1]) * 1000)
  const sure = s.match(/(?:try again|resets?)\s+in\s+(\d+)\s*(second|sec|minute|min|hour|hr)/i)
  if (sure) {
    const n = Number(sure[1]), b = sure[2].toLowerCase()
    return new Date(suan.getTime() + n * (b.startsWith('h') ? 3600e3 : b.startsWith('m') ? 60e3 : 1e3))
  }
  const saat = s.match(/(?:resets?|try again)\s+(?:at\s+)?(\d{1,2})(?::(\d{2}))?\s*(am|pm)?/i)
  if (saat) {
    const bolge = (s.match(/\(([A-Za-z_]+\/[A-Za-z_\/+-]+)\)/) || [])[1]
    if (bolge && bolge !== sistemBolgesi) return null
    let h = Number(saat[1]); const m = Number(saat[2] || 0), ap = (saat[3] || '').toLowerCase()
    if (m > 59 || (ap ? h < 1 || h > 12 : h > 23)) return null
    if (ap === 'pm' && h < 12) h += 12
    if (ap === 'am' && h === 12) h = 0
    const z = new Date(suan); z.setHours(h, m, 0, 0)
    if (z <= suan) z.setDate(z.getDate() + 1)
    return z
  }
  return null
}

// Bir denemenin sonucunu sınıflandırır (saf fonksiyon, test edilir).
//   sonTerminal: aracın SON terminal olayı ({tip:'basari'} | {tip:'hata', metin}) — Claude "result", Codex "turn.completed/failed"
//   geciciHata: terminal olmayan hata bildirimi (Codex "error"); sadece terminal olay yoksa kanıt olur
// Sıra: araç durdurulamadı > yerel ölümcül nedenler (iptal, zaman aşımı, kayıt/girdi hatası) > kesin başarı (başarı + çıkış 0)
//       > terminal servis hatası > (başarı yoksa) geçici hata / API metni / stderr > genel hata.
export function siniflandir({ durdurmaNedeni = null, cocukCanli = false, sonTerminal = null, geciciHata = '', apiMetni = '', stderr = '', kod = null }) {
  if (cocukCanli) return { tur: 'hata', not: 'Araç durdurulamadı; Görev Yöneticisi\'nden kapatılmalı' }
  if (durdurmaNedeni === 'iptal' || durdurmaNedeni === 'durdur') return { tur: 'durduruldu' }
  if (durdurmaNedeni === 'zaman-asimi') return { tur: 'hata', not: 'Çalışma süresi doldu, durduruldu' }
  if (durdurmaNedeni === 'kayit-hatasi') return { tur: 'hata', not: 'İş kaydı diske yazılamadı, güvenlik için durduruldu' }
  if (durdurmaNedeni === 'girdi-hatasi') return { tur: 'hata', not: 'İstem araca eksiksiz teslim edilemedi' }
  if (durdurmaNedeni) return { tur: 'hata', not: `Durduruldu: ${durdurmaNedeni}` }
  const basarili = sonTerminal?.tip === 'basari'
  if (basarili && kod === 0) return { tur: 'bitti' }
  const hataMetni = (sonTerminal?.tip === 'hata' ? String(sonTerminal.metin || 'hata') : '') ||
    (basarili ? '' : geciciHata || apiMetni || String(stderr).slice(-16384))
  if (hataMetni && GIRIS.test(hataMetni)) return { tur: 'giris' }
  if (hataMetni && KOTA.test(hataMetni)) return { tur: 'kota', metin: hataMetni }
  if (basarili) return { tur: 'hata', not: `Sonuç geldi ama araç ${kod} koduyla çıktı` }
  if (kod === 0) return { tur: 'hata', not: 'Araç bitti ama tamamlanma bilgisi göndermedi' }
  return { tur: 'hata', not: hataMetni ? hataMetni.slice(0, 200) : null }
}

// Kota geldiğinde yeniden denemek güvenli mi? Kimlik varsa kaldığı yerden sürer. Kimlik yoksa ancak araç hiçbir iş
// adımı atmadıysa baştan başlatılabilir; adım atıp kimliği kaybolmuş yarım iş ASLA baştan başlatılmaz.
export function yenidenDenenebilir({ sessionId, adimVar }) { return !!sessionId || !adimVar }

// Aynı iş kimliğiyle nöbetçi yeniden başlatılınca ne yapılacağı (saf fonksiyon, test edilir).
// Belirsizlik = ret: kayıt okunamıyorsa ya da adım atılıp atılmadığı bilinmiyorsa iş baştan başlatılmaz.
// Bir kez "yeniden başlatılamaz" işaretlenen iş, sonraki her çağrıda da reddedilir (engel kalıcı).
// Not: oturum kimliği her iki araçta da ilk çıktı satırıdır (Claude init, Codex thread.started) ve iş adımlarından önce
// kaydedilir; "kimliksiz ama adım atmış" durumu pratikte yalnız kayıt hatasında oluşur.
export function yenidenBaslatKarari(onceki, oncekiKilitVar) {
  if (!oncekiKilitVar) return { karar: 'yeni' }
  if (!onceki || typeof onceki !== 'object' || !onceki.durum) return { karar: 'reddet', neden: 'Önceki çalışmanın kaydı okunamadı; aynı iş iki kez yapılmasın diye başlatılmadı' }
  if (onceki.yenidenBaslatilamaz) return { karar: 'reddet', neden: onceki.not || 'Bu iş güvenli şekilde yeniden başlatılamaz' }
  if (onceki.durum === 'bitti') return { karar: 'zaten-bitti' }
  if (onceki.sessionId) return { karar: 'devam' }
  if (onceki.adimVar === false) return { karar: 'yeni' }
  return { karar: 'reddet', neden: 'Yarım kalmış işin oturum kimliği yok; aynı iş iki kez yapılmasın diye baştan başlatılmadı' }
}

function istemHazirla() {
  const imza = ARAC === 'codex' ? 'codex' : 'claude'
  if (GOREV) return `Görev paketini eksiksiz yap: \`${GOREV}\`. Önce AGENTS.md dosyasını oku ve uy: çalışırken proje kartına ` +
    `\`kilit: ${imza} <YYYY-AA-GG SS:DD>\` yaz, bitince temizle; sadece paketin izin verdiği dosyaları değiştir; devir notunu yaz ` +
    '(kartın "Son Devir" bölümü + Kayıt.md en üstü, Türkçe); görevin `durum: kontrol` yap ve "📥 Sonuç" bölümünü doldur; asla commit etme. ' +
    '"Güvenlik kapısı" bir işlemi reddederse tekrar deneme, etrafından dolaşma; istek numarasını devir notuna yaz. Her şeyi Türkçe yaz.'
  let t = fs.readFileSync(ISTEM_DOSYASI, 'utf8').replace(/\0/g, '').replace(/\r\n/g, '\n')
  // Kesmek yerine hata: sonundaki kısıtlar ("asla yayınlama" gibi) sessizce kaybolmasın
  if (t.length > ISTEM_SINIRI) kullanimHatasi(`istem ${t.length} karakter; sınır ${ISTEM_SINIRI}. Metin kesilmedi, iş başlatılmadı.`)
  if (!t.trim()) kullanimHatasi('istem boş')
  if (t.startsWith('-')) t = 'Görev: ' + t
  return t
}
const DEVAM_ISTEMI = 'Kullanım hakkın dolduğu için iş yarıda kaldı; şimdi hakkın yenilendi. Kaldığın yerden devam et ve işi bitir. ' +
  'Baştan başlama; önce ne yaptığını kısaca kontrol et, sonra eksik kalanı tamamla. Her şeyi Türkçe yaz.'

// Aracın komutu ve argümanları (kabuk yok). İstem argümanlarda YOK; standart girişten verilir.
export function komutHazirla(oturum, secenek = {}) {
  const { arac = ARAC, sahte = SAHTE, ekdizin = EKDIZIN, cd = CD, kasa = KASA } = secenek
  let komut, args
  if (arac === 'claude') {
    komut = sahte ? process.execPath : claudeBul()
    if (!komut) return null
    args = ['-p', '--output-format', 'stream-json', '--verbose', '--permission-mode', 'acceptEdits', '--permission-prompts', 'none']
    if (oturum) args.push('--resume', oturum)
    if (ekdizin && fs.existsSync(ekdizin)) args.push('--add-dir', ekdizin)
    // --allowedTools sadece "sormadan çalışabilir" listesidir, yasak değildir. Kullanılabilir araç
    // kümesi --tools ile daraltılır ve kabuk (Bash) her zaman, WebFetch --web yoksa açıkça yasaklanır; böylece
    // kullanıcının genel ayarlarında geniş izin olsa bile Claude işi bu araçları kullanamaz.
    args.push('--tools', CLAUDE_IZINLI, '--allowedTools', CLAUDE_IZINLI, '--disallowedTools', WEB ? 'Bash' : 'Bash,WebFetch')
    // Bağlı hesap eklentileri (MCP: Google Drive, Canva vb.) Claude işine açılmaz: sadece verilen yapılandırma geçerli, o da boş
    args.push('--strict-mcp-config')
  } else {
    komut = process.execPath
    args = [path.join(kasa, '_sistem', 'araclar', 'codex.mjs'), 'exec', '--json', '--skip-git-repo-check', '-C', cd || kasa, '-s', 'workspace-write']
    if (oturum) args.push('resume', oturum)
    args.push('-') // "-" = istemi standart girişten oku
  }
  if (args.some(x => YASAK_BAYRAK.test(x))) throw new Error('yasak bayrak')
  if (sahte) args = [sahte, ...(arac === 'codex' ? args.slice(1) : args)]
  return { komut, args }
}

// Süreç ağacını durdurur; sonucu (true/false) döner, asla fırlatmaz
function agaciOldur(pid) {
  return new Promise(coz => {
    if (!pid) return coz(false)
    if (process.platform !== 'win32') { try { process.kill(pid, 'SIGKILL'); return coz(true) } catch { return coz(!yasiyor(pid)) } }
    let k
    try { k = spawn('taskkill', ['/PID', String(pid), '/T', '/F'], { windowsHide: true, stdio: 'ignore' }) } catch (e) { olay('durdurma-hatasi', { hata: e.message }); return coz(false) }
    k.once('error', e => { olay('durdurma-hatasi', { hata: e.message }); coz(false) })
    // 128 = süreç zaten yok: bu da başarı sayılır
    k.once('close', kod => { if (kod !== 0 && kod !== 128) olay('durdurma-hatasi', { hata: `taskkill çıkış kodu ${kod}` }); coz(kod === 0 || kod === 128 || !yasiyor(pid)) })
  })
}

let aktifCocuk = null // sinyal gelirse (Ctrl+C gibi) durdurulacak araç

// Tek deneme: aracı çalıştırır, satırları dosyaya yazar, sonucu sınıflandırır.
// Dönüş: { tur, metin?, not?, adim, cocukCanliPid }
function calistir(istem, oturum) {
  return new Promise(coz => {
    let h
    try { h = komutHazirla(oturum) } catch (e) { olay('exit', { code: -1, hata: e.message }); return coz({ tur: 'hata', not: e.message }) }
    if (!h) { olay('exit', { code: -1, hata: 'no_cli' }); return coz({ tur: 'hata', not: 'Claude komut satırı bulunamadı' }) }
    const ortam = { ...process.env }
    for (const k of ['ANTHROPIC_API_KEY', 'ANTHROPIC_AUTH_TOKEN', 'CLAUDECODE']) delete ortam[k]
    // Kancalar (güvenlik kapısı, istek defteri) bu işin kasasını kesin bilsin: çalışma klasörü kasanın dışında bir
    // proje klasörü olsa ya da kullanıcı IKINCI_BEYIN_KASA'yı hiç ayarlamamış olsa bile onay istekleri ve Acil
    // Durdur, uygulamanın okuduğu bu kasaya bağlanır.
    ortam.IKINCI_BEYIN_KASA = KASA
    olay(oturum ? 'devam' : 'start', { args: h.args, cwd: CD || KASA, deneme: durum.deneme })
    // Kayıt yazılamıyorsa aracı hiç başlatma: izlenemeyen iş başarı sayılamaz
    if (kayitHatasi) return coz({ tur: 'hata', not: 'İş kaydı diske yazılamıyor; iş başlatılmadı', adim: false, cocukCanliPid: null })
    let c
    try { c = spawn(h.komut, h.args, { cwd: CD || KASA, env: ortam, windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] }) }
    catch (e) { olay('exit', { code: -1, hata: e.message }); return coz({ tur: 'hata', not: e.message }) }
    aktifCocuk = c
    kilitGuncelle({ cocukPid: c.pid || null, cocukBas: simdi() })

    let sonTerminal = null, geciciHata = '', apiMetni = '', stderr = '', adim = false
    let durdurmaNedeni = null, durdurmaBasi = 0, sonOldurme = 0, oldurmeSuruyor = false, bitti = false
    const oldur = () => {
      if (bitti || oldurmeSuruyor) return
      // Yineleme aralığı burada uygulanır: sürekli iptalde her 2 sn'de bir değil, 5 sn'de bir dener
      if (sonOldurme && Date.now() - sonOldurme < OLDURME_ARALIK_MS) return
      oldurmeSuruyor = true; sonOldurme = Date.now()
      agaciOldur(c.pid).then(ok => { oldurmeSuruyor = false; if (!ok) olay('durdurma-basarisiz', { pid: c.pid }) })
    }
    const durdur = neden => { if (!durdurmaNedeni) { durdurmaNedeni = neden; durdurmaBasi = Date.now() } oldur() }
    const adimGordum = () => { if (!adim) { adim = true; durumYaz({ adimVar: true }) } }

    c.stdin.on('error', e => { olay('girdi-hatasi', { hata: e.message }); durdur('girdi-hatasi') })
    try { c.stdin.end(istem, 'utf8') } catch (e) { olay('girdi-hatasi', { hata: e.message }); durdur('girdi-hatasi') }

    readline.createInterface({ input: c.stdout }).on('line', l => {
      if (!l.trim()) return
      try { fs.appendFileSync(JSONL, l + '\n') } catch (e) { kayitHatasi ||= e.message; durdur('kayit-hatasi'); return }
      let j; try { j = JSON.parse(l) } catch { return }
      if (ARAC === 'claude') {
        if (j.type === 'system' && j.subtype === 'init' && j.session_id) durumYaz({ sessionId: j.session_id })
        if (j.type === 'assistant') for (const b of j.message?.content || []) {
          if (b.type === 'tool_use') adimGordum()
          // API hatası bazen sadece asistan mesajı olarak gelir; başarı yoksa yedek kanıt
          if (b.type === 'text' && /^(API Error|Invalid API key|Claude AI usage limit|You've hit)/i.test(b.text || '')) apiMetni = b.text
        }
        if (j.type === 'result') sonTerminal = j.is_error ? { tip: 'hata', metin: String(j.result || 'hata') } : { tip: 'basari' }
      } else {
        if (j.type === 'thread.started' && j.thread_id) durumYaz({ sessionId: j.thread_id })
        if (j.type === 'turn.started') { sonTerminal = null; geciciHata = '' } // her tur temiz başlar
        if ((j.type === 'item.started' || j.type === 'item.completed') && CODEX_ADIM.test(j.item?.type || '')) adimGordum()
        if (j.type === 'error') geciciHata = String(j.message || 'hata')
        if (j.type === 'turn.completed') { sonTerminal = { tip: 'basari' }; geciciHata = '' }
        if (j.type === 'turn.failed') sonTerminal = { tip: 'hata', metin: String(j.error?.message || geciciHata || 'hata') }
      }
    })
    // stderr satır satır: parçalanan mesajlar birleşik kalır; son 16 KB saklanır
    readline.createInterface({ input: c.stderr }).on('line', l => {
      stderr = (stderr + '\n' + l).slice(-16384)
      olay('stderr', { text: l.slice(0, 2000) })
    })

    const son = (kod, ek = {}) => {
      if (bitti) return; bitti = true
      clearTimeout(zaman); clearInterval(bekci)
      aktifCocuk = null
      const cocukCanli = !!ek.cocukCanli
      olay('exit', { code: kod, ...ek })
      // Bekçi fark etmeden kapanan araçta da kayıt hatası sonuca taşınır
      if (kayitHatasi && !durdurmaNedeni) durdurmaNedeni = 'kayit-hatasi'
      if (!cocukCanli) kilitGuncelle({ cocukPid: null, cocukBas: null })
      const r = siniflandir({ durdurmaNedeni, cocukCanli, sonTerminal, geciciHata, apiMetni, stderr, kod })
      coz({ ...r, adim, cocukCanliPid: cocukCanli ? c.pid : null })
    }
    const zaman = setTimeout(() => { olay('timeout'); durdur('zaman-asimi') }, SURE_DK * 60 * 1000)
    // 2 saniyede bir: iptal/acil durdurma, kayıt hatası; durdurma sürüyorsa öldürmeyi yinele, 90 sn'de pes et
    const bekci = setInterval(() => {
      if (durmali()) { const neden = fs.existsSync(IPTAL) ? 'iptal' : 'durdur'; if (!durdurmaNedeni) olay(neden === 'iptal' ? 'cancel' : 'durdur'); durdur(neden) }
      if (kayitHatasi && !durdurmaNedeni) durdur('kayit-hatasi')
      if (durdurmaNedeni && !bitti) {
        if (Date.now() - durdurmaBasi > OLDURME_PES_MS) son(-1, { hata: 'sonlandirilamadi', cocukCanli: yasiyor(c.pid) })
        else oldur()
      }
    }, 2000)
    c.on('error', e => son(-1, { hata: String(e.message) }))
    c.on('close', kod => son(kod))
  })
}

const uyu = ms => new Promise(r => setTimeout(r, ms))

// Uyku engeli (EK-v2.1 §2b): nöbetçi yaşadıkça (iş çalışırken VE kota beklerken) bilgisayar uyku moduna geçmesin; yoksa
// kota sıfırlanma saatinde iş devam edemez. Küçük bir PowerShell yardımcısı SetThreadExecutionState ile "iş var" der ve
// nöbetçi süreci bitene kadar bekler; nöbetçi herhangi bir şekilde kapanınca yardımcı da çıkar ve engel kendiliğinden kalkar.
// Windows güç ayarları DEĞİŞTİRİLMEZ. --uyku-serbest ile kapatılır; deneme (--sahte) modunda hiç başlatılmaz.
function uykuEngeliBaslat() {
  if (process.platform !== 'win32' || SAHTE || process.argv.includes('--uyku-serbest')) return null
  const betik = 'Add-Type -Namespace Uyanik -Name Guc -MemberDefinition \'[DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint f);\'; ' +
    '[void][Uyanik.Guc]::SetThreadExecutionState([uint32]2147483649); ' + // 0x80000001 = ES_CONTINUOUS + ES_SYSTEM_REQUIRED
    `try { Wait-Process -Id ${process.pid} -ErrorAction Stop } catch {}`
  try {
    const p = spawn('powershell', ['-NoProfile', '-NonInteractive', '-WindowStyle', 'Hidden', '-Command', betik], { windowsHide: true, stdio: 'ignore' })
    p.on('error', () => {})
    olay('uyku-engeli', { pid: p.pid })
    return p
  } catch { return null }
}

async function main() {
  const ilkIstem = istemHazirla() // kilitten ÖNCE: yanlış kullanım kilit almadan çıkar
  fs.mkdirSync(KLASOR, { recursive: true })
  const k = kilitAl()
  if (k.mesgul) { console.error(`bu iş zaten çalışıyor (${k.mesgul})`); return 2 }
  let cocukCanliPid = null
  const uykuYardimcisi = uykuEngeliBaslat()
  process.on('exit', () => { try { uykuYardimcisi?.kill() } catch {} })
  const kalp = setInterval(() => { kilitGuncelle(); durumYaz({}) }, KALP_MS)
  // Ctrl+C / kapatma sinyali: aracı durdur, durumu yaz, kilidi bırak. Tek sefer çalışır; çocuk beklemeden önce yerel
  // değişkene alınır (bekleme sırasında aktifCocuk boşalabilir); hata olsa bile kilit bırakılıp çıkılır.
  let kapaniyor = false
  const sinyal = async ad => {
    if (kapaniyor) return; kapaniyor = true
    try {
      olay('sinyal', { ad })
      const c = aktifCocuk
      if (c && c.pid) {
        let ok = false
        for (let i = 0; i < 3 && !ok; i++) { ok = await agaciOldur(c.pid); if (!ok) await uyu(2000) }
        if (!ok && yasiyor(c.pid)) cocukCanliPid = c.pid
      }
      durumYaz({ durum: 'durduruldu', exitCode: 3, not: `Nöbetçi kapatıldı (${ad})` })
    } catch {} finally { clearInterval(kalp); kilitBirak(cocukCanliPid); process.exit(3) }
  }
  for (const ad of ['SIGINT', 'SIGTERM', 'SIGBREAK', 'SIGHUP']) { try { process.on(ad, () => { sinyal(ad).catch(() => process.exit(3)) }) } catch {} }
  try {
    // Aynı iş kimliği daha önce çalıştıysa (bilgisayar kapandı, uygulama yeniden başlattı vb.) ne yapılacağına karar ver
    let onceki = null; if (k.oncekiVar) { try { onceki = JSON.parse(fs.readFileSync(NOBET, 'utf8')) } catch {} }
    const karar = yenidenBaslatKarari(onceki, k.oncekiVar)
    if (karar.karar === 'reddet') {
      durum = { ...durum, sessionId: onceki?.sessionId ?? null, adimVar: onceki?.adimVar ?? null, deneme: onceki?.deneme || 0 }
      durumYaz({ durum: 'hata', exitCode: 1, not: karar.neden, yenidenBaslatilamaz: true })
      return 1
    }
    if (karar.karar === 'zaten-bitti') { olay('zaten-bitti'); console.error('bu iş zaten bitmiş; yeniden çalıştırılmadı'); return 0 }
    if (karar.karar === 'devam') {
      durum = { ...durum, sessionId: onceki.sessionId, deneme: onceki.deneme || 0, adimVar: !!onceki.adimVar }
      olay('devralindi', { eskiPid: onceki.pid, sessionId: onceki.sessionId })
    }
    const baslangic = durum.deneme
    for (let i = baslangic + 1; i <= baslangic + DENEME; i++) {
      if (durmali()) { durumYaz({ durum: 'durduruldu', exitCode: 3 }); return 3 }
      durumYaz({ durum: 'calisiyor', deneme: i, bekleUntil: null })
      const r = await calistir(durum.sessionId ? DEVAM_ISTEMI : ilkIstem, durum.sessionId)
      cocukCanliPid = r.cocukCanliPid || null
      // Başarı ancak "bitti" durumu da diske yazılabildiyse başarıdır
      if (r.tur === 'bitti') { durumYaz({ durum: 'bitti', exitCode: 0 }); return kayitHatasi ? 1 : 0 }
      if (r.tur === 'durduruldu') { durumYaz({ durum: 'durduruldu', exitCode: 3 }); return 3 }
      if (r.tur === 'giris') {
        durumYaz({ durum: 'giris', exitCode: 4, not: ARAC === 'codex' ? 'Codex uygulamasında bir kez giriş yap.' : 'Claude komut satırında bir kez /login yap.' })
        return 4
      }
      if (r.tur === 'hata') { durumYaz({ durum: 'hata', exitCode: 1, not: r.not || null }); return 1 }
      // Kota. Kimliği kaybolmuş yarım iş baştan başlatılmaz; son deneme hakkıysa boşuna beklenmez.
      if (!yenidenDenenebilir({ sessionId: durum.sessionId, adimVar: durum.adimVar || r.adim })) {
        durumYaz({ durum: 'hata', exitCode: 1, not: 'Kota geldi ama oturum kimliği yok ve iş yarıda; baştan başlatılmadı', adimVar: true, yenidenBaslatilamaz: true })
        return 1
      }
      if (i === baslangic + DENEME) break
      const z = sifirlanmaZamani(r.metin)
      const hedef = z ? new Date(z.getTime() + PAY_SN * 1000) : new Date(Date.now() + BEKLE_DK * 60000)
      const oncekiSinyal = sinyalOku() // bekleme duyurulmadan ÖNCE: aradaki tıklama kaybolmasın
      durumYaz({ durum: 'kota-bekliyor', bekleUntil: hedef.toISOString() })
      olay('kota-bekliyor', { until: hedef.toISOString(), metin: String(r.metin).slice(0, 300) })
      while (Date.now() < hedef.getTime()) {
        // 2 saniyede bir iptal/acil durdurma ve "Şimdi dene" kontrolü (uzun bekleyişte bile hızlı tepki)
        if (durmali()) { durumYaz({ durum: 'durduruldu', exitCode: 3 }); olay(fs.existsSync(IPTAL) ? 'cancel' : 'durdur'); return 3 }
        const sinyal = sinyalOku()
        if (sinyal !== null && sinyal !== oncekiSinyal) { olay('simdi-dene'); break }
        await uyu(Math.min(2000, Math.max(200, hedef.getTime() - Date.now())))
      }
    }
    durumYaz({ durum: 'hata', exitCode: 5, not: `Kota ${DENEME} denemede açılmadı` })
    return 5
  } finally { clearInterval(kalp); kilitBirak(cocukCanliPid) }
}

if (DOGRUDAN) {
  main().then(k => process.exit(k)).catch(e => { durumYaz({ durum: 'hata', exitCode: 1, not: String(e.message) }); kilitBirak(); process.exit(1) })
}
