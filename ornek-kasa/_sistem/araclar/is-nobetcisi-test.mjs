// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
// İş nöbetçisi testleri (Claude ve Codex): kota → bekle → kaldığı yerden devam, iptal, "Şimdi dene", giriş hatası,
// yanlış kota alarmı, çift başlatma, yarıda kalan işi devralma, saat ayrıştırma, sınıflandırma.
// Çalıştır: node _sistem/araclar/is-nobetcisi-test.mjs   (gerçek kota harcamaz; iş dosyaları %TEMP% içine yazılır)
import { spawnSync, spawn } from 'node:child_process'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

const BURASI = path.dirname(fileURLToPath(import.meta.url))
const NOBET = path.join(BURASI, 'is-nobetcisi.mjs')
const SAHTE = path.join(BURASI, 'is-nobetcisi-sahte.mjs')
const KLASOR = fs.mkdtempSync(path.join(os.tmpdir(), 'is-nobetcisi-test-'))
const ISTEM = path.join(KLASOR, 'istem.txt')
fs.writeFileSync(ISTEM, 'Deneme işi: sadece AGENTS.md oku. Asla yayınlama.')
let hata = 0, gecen = 0
const kontrol = (ad, kosul, ek = '') => { console.log(`${kosul ? 'GEÇTİ' : 'KALDI'}  ${ad}${ek ? ' — ' + ek : ''}`); if (kosul) gecen++; else hata++ }
const oku = is => {
  const j = path.join(KLASOR, `${is}.jsonl`), n = path.join(KLASOR, `${is}.nobet.json`)
  return {
    satirlar: fs.existsSync(j) ? fs.readFileSync(j, 'utf8').trim().split('\n').filter(Boolean).map(l => JSON.parse(l)) : [],
    durum: fs.existsSync(n) ? JSON.parse(fs.readFileSync(n, 'utf8')) : {},
  }
}
const olaylar = is => oku(is).satirlar.filter(s => s.type === 'ordinaryunus').map(s => s.event).join(',')
const argsYap = (arac, is, ek = []) => [NOBET, '--arac', arac, '--is', is, '--istem-dosyasi', ISTEM, '--klasor', KLASOR, '--sahte', SAHTE, '--pay-sn', '1', ...ek]
const calistir = (arac, is, mod, ek = []) => spawnSync(process.execPath, argsYap(arac, is, ek), { encoding: 'utf8', env: { ...process.env, SAHTE_MOD: mod }, timeout: 90000 })
// Arka planda başlatılan her nöbetçi kaydedilir; test sonunda (başarılı ya da değil) hepsi ve çocukları kapatılır.
// Yoksa art arda çalıştırmada önceki koşunun süreçleri sonrakine karışır.
const baslatilanlar = []
const baslat = (arac, is, mod, ek = []) => { const c = spawn(process.execPath, argsYap(arac, is, ek), { env: { ...process.env, SAHTE_MOD: mod }, stdio: 'ignore' }); baslatilanlar.push(c); return c }
function artiklariTemizle() {
  const pidler = new Set(baslatilanlar.filter(c => c.exitCode === null && c.signalCode === null).map(c => c.pid))
  // Test klasöründeki bütün kilitlerde ve durum dosyalarında kayıtlı süreçler (nöbetçi ve sahte araç)
  for (const f of fs.readdirSync(KLASOR)) {
    if (!/\.kilit\.\d+$|\.nobet\.json$/.test(f)) continue
    try { const k = JSON.parse(fs.readFileSync(path.join(KLASOR, f), 'utf8')); for (const p of [k.pid, k.cocukPid]) if (p) pidler.add(p) } catch {}
  }
  for (const p of pidler) { if (p === process.pid) continue; try { spawnSync('taskkill', ['/PID', String(p), '/T', '/F'], { stdio: 'ignore', windowsHide: true }) } catch {} }
}
process.on('exit', artiklariTemizle)
const bekle = ms => new Promise(r => setTimeout(r, ms))
// Süreç zaten çıktıysa bekleme (çıkış olayı kaçmış olur)
const cikis = c => (c.exitCode !== null || c.signalCode !== null) ? Promise.resolve(c.exitCode) : new Promise(r => c.on('exit', r))

for (const [arac, oturum] of [['claude', 'sahte-oturum-1'], ['codex', 'sahte-codex-1']]) {
  console.log(`--- ${arac}`)
  // 1) kota → bekle → aynı oturumla devam → bitti
  {
    const is = `t-${arac}-kota-${Date.now()}`
    const bas = Date.now(); const r = calistir(arac, is, 'kota-sonra-bitti'); const sure = (Date.now() - bas) / 1000
    const { satirlar, durum } = oku(is)
    kontrol('kota sonrası bitti (çıkış 0)', r.status === 0, `çıkış ${r.status} ${String(r.stderr).slice(0, 200)}`)
    kontrol('olay sırası start → kota-bekliyor → devam', olaylar(is).includes('start,exit,kota-bekliyor,devam,exit'), olaylar(is))
    kontrol('sıfırlanmayı bekledi (≥3 sn)', sure >= 3, `${sure.toFixed(1)} sn`)
    kontrol('durum: bitti, 2 deneme, oturum aynı', durum.durum === 'bitti' && durum.deneme === 2 && durum.sessionId === oturum, `${durum.durum} ${durum.deneme} ${durum.sessionId}`)
    const devamArg = (satirlar.find(s => s.event === 'devam')?.args || []).join(' ')
    kontrol('devam çağrısı aynı oturumu kullanıyor', devamArg.includes(arac === 'claude' ? `--resume ${oturum}` : `resume ${oturum}`), devamArg.slice(-120))
    kontrol('devam istemi standart girişten gitti', satirlar.some(s => /devam istemi alındı: evet/.test(s.result || s.item?.text || '')))
    const tumArg = satirlar.filter(s => s.args).flatMap(s => s.args).join(' ')
    kontrol('istem argümanlarda ve kayıtta YOK', !tumArg.includes('Deneme işi') && !fs.readFileSync(path.join(KLASOR, `${is}.jsonl`), 'utf8').includes('Deneme işi'))
    kontrol('izin atlama / tehlikeli bayrak yok', !/dangerously|bypassPermissions|danger-full-access/.test(tumArg))
    if (arac === 'claude') {
      const a0 = satirlar.find(s => s.args).args
      const izin = a0[a0.indexOf('--allowedTools') + 1], kume = a0[a0.indexOf('--tools') + 1], yasak = a0[a0.indexOf('--disallowedTools') + 1]
      kontrol('Claude işinde izin ve araç listesinde kabuk (Bash) ve WebFetch yok, ikisi de açıkça yasak (varsayılan)',
        !/Bash|WebFetch/.test(izin) && !/Bash|WebFetch/.test(kume) && yasak === 'Bash,WebFetch', `${izin} | ${yasak}`)
    }
    if (arac === 'codex') kontrol('Codex hep -s workspace-write ve istem "-" (stdin)', satirlar.filter(s => s.args).every(s => s.args.join(' ').includes('-s workspace-write') && s.args.at(-1) === '-'))
    kontrol('iş bitince kilit bırakıldı (pid 0)', JSON.parse(fs.readFileSync(path.join(KLASOR, `${is}.kilit.1`), 'utf8')).pid === 0)
  }
  // 2) yanlış kota alarmı yok: iş başarılı, hata akışında "429 rate limit … try again at" yazıyor
  {
    const is = `t-${arac}-yanlis-${Date.now()}`
    const r = calistir(arac, is, 'basari-stderr-429')
    kontrol('başarılı iş hata akışındaki "429/rate limit/unauthorized" yüzünden kota/giriş SAYILMADI', r.status === 0 && oku(is).durum.durum === 'bitti', `kod ${r.status} ${oku(is).durum.durum}`)
  }
  // 3) başarı olayı + çıkış 1 → hata; hiç tamamlanma bilgisi yok + çıkış 0 → hata
  {
    const a = `t-${arac}-kod1-${Date.now()}`, b = `t-${arac}-bos-${Date.now()}`
    const ra = calistir(arac, a, 'basari-kod1'), rb = calistir(arac, b, 'bos-kod0')
    kontrol('başarı olayı ama çıkış 1 → hata (bitti sayılmadı)', ra.status === 1 && oku(a).durum.durum === 'hata', `${ra.status} ${oku(a).durum.durum}`)
    kontrol('tamamlanma bilgisi yok, çıkış 0 → hata', rb.status === 1 && oku(b).durum.durum === 'hata', `${rb.status} ${oku(b).durum.durum}`)
  }
  // 4) kota beklerken iptal → çıkış 3, hızlı
  {
    const is = `t-${arac}-iptal-${Date.now()}`
    const c = baslat(arac, is, 'kota-uzun'); const bas = Date.now()
    await bekle(3000)
    const ara = oku(is).durum
    kontrol('iptalden önce durum kota-bekliyor ve saat yazılı', ara.durum === 'kota-bekliyor' && !!ara.bekleUntil, `${ara.durum} ${ara.bekleUntil}`)
    fs.writeFileSync(path.join(KLASOR, `${is}.iptal`), '')
    const kod = await cikis(c)
    kontrol('kota beklerken iptal → hemen çıktı (kod 3)', kod === 3 && Date.now() - bas < 15000, `kod ${kod}, ${((Date.now() - bas) / 1000).toFixed(1)} sn`)
  }
  // 5) ÇALIŞIRKEN iptal → araç öldürüldü, durum durduruldu (hata değil)
  {
    const is = `t-${arac}-calisirken-${Date.now()}`
    const c = baslat(arac, is, 'uzun-calis'); const bas = Date.now()
    await bekle(1500)
    fs.writeFileSync(path.join(KLASOR, `${is}.iptal`), '')
    const kod = await cikis(c)
    kontrol('çalışırken iptal → araç durduruldu (kod 3, <20 sn)', kod === 3 && oku(is).durum.durum === 'durduruldu' && Date.now() - bas < 20000, `kod ${kod} ${oku(is).durum.durum} ${((Date.now() - bas) / 1000).toFixed(1)} sn`)
  }
  // 6) "Şimdi dene": beklemeyi keser, yeniden dener; aynı değeri tekrar yazmak ikinci kez tetiklemez
  {
    const is = `t-${arac}-simdi-${Date.now()}`
    const c = baslat(arac, is, 'kota-uzun')
    await bekle(2500)
    fs.writeFileSync(path.join(KLASOR, `${is}.simdi`), 'tik-1')
    await bekle(4500)
    kontrol('"Şimdi dene" beklemeyi kesip yeniden denedi', olaylar(is).includes('kota-bekliyor,simdi-dene,devam') && oku(is).durum.deneme === 2, olaylar(is))
    await bekle(2500)
    kontrol('aynı sinyal ikinci kez tetiklemedi', (olaylar(is).match(/simdi-dene/g) || []).length === 1 && oku(is).durum.durum === 'kota-bekliyor', olaylar(is))
    fs.writeFileSync(path.join(KLASOR, `${is}.iptal`), ''); await cikis(c)
  }
  // 7) giriş gerekli → çıkış 4, bekleme yok
  {
    const is = `t-${arac}-giris-${Date.now()}`
    const r = calistir(arac, is, 'giris')
    kontrol('giriş hatası: kod 4, durum giris', r.status === 4 && oku(is).durum.durum === 'giris', `kod ${r.status} ${oku(is).durum.durum}`)
  }
}

// 7b) geçici kota bildirimi + başarılı tur + çıkış 1 → hata (kota beklemesine GİRMEZ); kimliksiz yarım iş kota → baştan başlamaz
for (const arac of ['claude', 'codex']) {
  const a = 't-' + arac + '-gecici-' + Date.now()
  const r = calistir(arac, a, 'gecici-kota-basari-kod1')
  kontrol(arac + ': geçici kota + başarılı tur + çıkış 1 → hata, beklemeye girmedi', r.status === 1 && oku(a).durum.durum === 'hata' && !olaylar(a).includes('kota-bekliyor'), r.status + ' ' + oku(a).durum.durum + ' ' + olaylar(a))
  const b = 't-' + arac + '-kimliksiz-' + Date.now()
  const r2 = calistir(arac, b, 'adim-kimliksiz-kota')
  kontrol(arac + ': kimliksiz + iş adımı atmış + kota → baştan BAŞLATILMADI (hata)', r2.status === 1 && oku(b).durum.durum === 'hata' && (olaylar(b).match(/start/g) || []).length === 1, r2.status + ' ' + oku(b).durum.durum + ' ' + olaylar(b))
}

// 8) aynı iş iki kez başlatılamaz; yarıda kalan işi yeni nöbetçi kaldığı yerden devralır
{
  console.log('--- kilit ve devralma')
  const is = `t-cift-${Date.now()}`
  const c = baslat('claude', is, 'uzun-calis')
  await bekle(1500)
  const r = calistir('claude', is, 'uzun-calis')
  kontrol('aynı iş ikinci kez başlatılamadı (kod 2)', r.status === 2 && /zaten çalışıyor/.test(r.stderr), `kod ${r.status} ${String(r.stderr).trim()}`)
  fs.writeFileSync(path.join(KLASOR, `${is}.iptal`), ''); await cikis(c)

  const esz = 't-esz-' + Date.now()
  const cocuklar = Array.from({ length: 5 }, () => baslat('claude', esz, 'uzun-calis'))
  await bekle(3000)
  const baslayan = oku(esz).satirlar.filter(s => s.event === 'start').length
  kontrol('5 nöbetçi aynı anda başlatıldı → sadece 1 tanesi işi başlattı', baslayan === 1, baslayan + ' başlangıç')
  fs.writeFileSync(path.join(KLASOR, esz + '.iptal'), ''); await Promise.all(cocuklar.map(cikis))

  const is2 = 't-devral-' + Date.now()
  fs.writeFileSync(path.join(KLASOR, is2 + '.kilit.1'), JSON.stringify({ pid: 999999, t: '2026-09-24T01:00:00Z' }))
  fs.writeFileSync(path.join(KLASOR, `${is2}.nobet.json`), JSON.stringify({ is: is2, durum: 'kota-bekliyor', sessionId: 'eski-oturum-7', deneme: 3, pid: 999999 }))
  const r2 = calistir('claude', is2, 'kota-sonra-bitti')
  const s2 = oku(is2)
  const ilkCagri = (s2.satirlar.find(s => s.event === 'devam' || s.event === 'start') || {})
  kontrol('ölü nöbetçinin işi devralındı ve kaldığı oturumdan sürdü', r2.status === 0 && ilkCagri.event === 'devam' && (ilkCagri.args || []).join(' ').includes('--resume eski-oturum-7') && s2.durum.deneme === 4,
    `kod ${r2.status} ${ilkCagri.event} deneme ${s2.durum.deneme}`)
}

// 8b) Ek senaryolar: boş kilit, bitmiş iş, kalıcı ret, --web
{
  console.log('--- ek senaryolar: boş kilit, bitmiş iş, kalıcı ret, --web')
  const bos = 't-boskilit-' + Date.now()
  fs.writeFileSync(path.join(KLASOR, bos + '.kilit.1'), '')
  const r1 = calistir('claude', bos, 'kota-sonra-bitti')
  kontrol('okunamayan (boş) kilit → iş başlatılmadı (kod 2), devralınmadı (T1)', r1.status === 2 && !olaylar(bos).includes('start') && /okunamadı/.test(r1.stderr), r1.status + ' ' + String(r1.stderr).trim())
  const bitmis = 't-bitmis-' + Date.now()
  const r2 = calistir('claude', bitmis, 'kota-sonra-bitti')
  const once = oku(bitmis).satirlar.filter(s => s.event === 'start' || s.event === 'devam').length
  const r3 = calistir('claude', bitmis, 'kota-sonra-bitti')
  const sonra = oku(bitmis).satirlar.filter(s => s.event === 'start' || s.event === 'devam').length
  kontrol('bitmiş iş yeniden çağrılınca hiçbir şey başlatılmadı (kod 0)', r2.status === 0 && r3.status === 0 && once === sonra && oku(bitmis).durum.durum === 'bitti', once + ' → ' + sonra)
  const ret = 't-kaliciret-' + Date.now()
  fs.writeFileSync(path.join(KLASOR, ret + '.kilit.1'), JSON.stringify({ pid: 0, t: '2026-09-24T01:00:00Z' }))
  fs.writeFileSync(path.join(KLASOR, ret + '.nobet.json'), JSON.stringify({ is: ret, durum: 'calisiyor', sessionId: null, adimVar: true }))
  const r4 = calistir('claude', ret, 'kota-sonra-bitti')
  const r5 = calistir('claude', ret, 'kota-sonra-bitti')
  kontrol('kimliksiz yarım iş: birinci ve İKİNCİ çağrıda da reddedildi, hiç başlatılmadı (T4)', r4.status === 1 && r5.status === 1 && !olaylar(ret).includes('start') && oku(ret).durum.yenidenBaslatilamaz === true, r4.status + ' ' + r5.status + ' ' + olaylar(ret))
  const web = 't-web-' + Date.now()
  calistir('claude', web, 'kota-sonra-bitti', ['--web'])
  const wa = oku(web).satirlar.filter(s => s.args).map(s => s.args.join(' ')).join(' | ')
  kontrol('--web ile WebFetch serbest, Bash yine yasak', wa.includes('--tools Read,Grep,Glob,Edit,Write,TodoWrite,WebSearch,WebFetch') && wa.includes('--disallowedTools Bash ') , wa.slice(0, 160))
}

// 9) yanlış kullanım
{
  console.log('--- yanlış kullanım')
  const uzun = path.join(KLASOR, 'uzun.txt'); fs.writeFileSync(uzun, 'x'.repeat(3990) + ' Asla yayınlama.')
  const r = spawnSync(process.execPath, [NOBET, '--arac', 'claude', '--is', 'uzun-istem-1', '--istem-dosyasi', uzun, '--klasor', KLASOR, '--sahte', SAHTE], { encoding: 'utf8' })
  kontrol('4000 karakteri aşan istem kesilmedi, reddedildi (kod 2)', r.status === 2 && /kesilmedi/.test(r.stderr), String(r.stderr).trim())
  const r2 = calistir('claude', 'yanlis-sayi-1', 'kota-sonra-bitti', ['--deneme', 'sayi-degil'])
  kontrol('--deneme sayı değilse kullanım hatası (kod 2)', r2.status === 2, `kod ${r2.status}`)
  const r3 = calistir('codex', 'son-deneme-1', 'kota-uzun', ['--deneme', '1'])
  kontrol('son denemede kota → beklemeden kod 5', r3.status === 5 && !olaylar('son-deneme-1').includes('kota-bekliyor'), `kod ${r3.status} ${olaylar('son-deneme-1')}`)
}

// 10) saat ayrıştırma ve sınıflandırma (saf fonksiyonlar)
{
  console.log('--- saat ayrıştırma ve sınıflandırma')
  const { sifirlanmaZamani, siniflandir } = await import(pathToFileURL(NOBET).href)
  const suan = new Date(2026, 8, 24, 1, 10, 0), ist = 'Europe/Istanbul'
  kontrol('Claude saniye biçimi', sifirlanmaZamani('Claude AI usage limit reached|1759201200', suan, ist)?.getTime() === 1759201200000)
  const a = sifirlanmaZamani("You've hit your limit · resets 3am (Europe/Istanbul)", suan, ist)
  kontrol('"resets 3am (Europe/Istanbul)" İstanbul bilgisayarında → bugün 03:00', a && a.getHours() === 3 && a.getDate() === 24, String(a))
  kontrol('mesajdaki saat dilimi bilgisayarınkinden farklıysa null (varsayılan bekleme)', sifirlanmaZamani('resets 3am (Europe/Istanbul)', suan, 'UTC') === null)
  const b = sifirlanmaZamani('try later, resets at 1:05 AM', suan, ist)
  kontrol('geçmiş saat → yarın', b && b.getDate() === 25 && b.getHours() === 1 && b.getMinutes() === 5, String(b))
  const c = sifirlanmaZamani('You’ve hit your usage limit. Upgrade to Pro (https://chatgpt.com/explore/pro) … or try again at 3:09 AM.', suan, ist)
  kontrol('gerçek Codex mesajı → bugün 03:09', c && c.getHours() === 3 && c.getMinutes() === 9 && c.getDate() === 24, String(c))
  kontrol('12 AM → 00:00, 12 PM → 12:00', sifirlanmaZamani('resets 12 AM', suan, ist)?.getHours() === 0 && sifirlanmaZamani('resets 12:30 PM', suan, ist)?.getHours() === 12)
  kontrol('geçersiz saat (99:99, 13 PM) → null', sifirlanmaZamani('resets 99:99', suan, ist) === null && sifirlanmaZamani('resets 13 PM', suan, ist) === null)
  kontrol('"try again in 12 minutes"', sifirlanmaZamani('Rate limited. Please try again in 12 minutes.', suan, ist)?.getTime() - suan.getTime() === 12 * 60e3)
  kontrol('saat yoksa null', sifirlanmaZamani('usage limit reached', suan, ist) === null)
  const B = { tip: 'basari' }
  kontrol('sınıflandırma: başarı+0 kod, stderr\'de 429 → bitti', siniflandir({ sonTerminal: B, kod: 0, stderr: '429 rate limit unauthorized' }).tur === 'bitti')
  kontrol('sınıflandırma: iptal nedeni kalıcı → durduruldu', siniflandir({ durdurmaNedeni: 'iptal', sonTerminal: B, kod: 1 }).tur === 'durduruldu')
  kontrol('sınıflandırma: zaman aşımı + başarı → hata', siniflandir({ durdurmaNedeni: 'zaman-asimi', sonTerminal: B, kod: 1 }).tur === 'hata')
  kontrol('sınıflandırma: servis hatası kota → kota', siniflandir({ sonTerminal: { tip: 'hata', metin: 'You’ve hit your usage limit … try again at 3:09 AM.' }, kod: 1 }).tur === 'kota')
  kontrol('sınıflandırma: başarı yoksa parçalı stderr birleşik okunur → kota', siniflandir({ stderr: 'rate li\nmit reached', kod: 1 }).tur !== 'bitti')
  kontrol('sınıflandırma: kayıt hatası + başarı → hata (başarıya dönmez)', siniflandir({ durdurmaNedeni: 'kayit-hatasi', sonTerminal: B, kod: 0 }).tur === 'hata')
  kontrol('sınıflandırma: girdi hatası + başarı → hata', siniflandir({ durdurmaNedeni: 'girdi-hatasi', sonTerminal: B, kod: 0 }).tur === 'hata')
  kontrol('sınıflandırma: durdurulamayan araç → hata', siniflandir({ durdurmaNedeni: 'iptal', cocukCanli: true }).tur === 'hata')
  kontrol('sınıflandırma: son terminal olay hata → hata', siniflandir({ sonTerminal: { tip: 'hata', metin: 'x' }, kod: 0 }).tur === 'hata')
  const { sahipCanli, yenidenDenenebilir, yenidenBaslatKarari, komutHazirla } = await import(pathToFileURL(NOBET).href)
  const canli = () => true, olu = () => false
  const bas = '2026-09-24T10:00:00.000Z', basMs = Date.parse(bas)
  kontrol('kilit: pid yaşıyor ve başlangıç anı aynı → canlı', sahipCanli({ pid: 7, bas }, { yasiyorMu: canli, baslangicOku: () => basMs + 800, benimPid: 1 }) === true)
  kontrol('kilit: kalp atışı çok eski ama süreç aynı (duraklamış) → YİNE canlı (T1)', sahipCanli({ pid: 7, bas, t: '2026-09-20T00:00:00Z' }, { yasiyorMu: canli, baslangicOku: () => basMs, benimPid: 1 }) === true)
  kontrol('kilit: pid yaşıyor ama başlangıç anı farklı (pid yeniden kullanılmış) → bayat', sahipCanli({ pid: 7, bas }, { yasiyorMu: canli, baslangicOku: () => basMs + 3600e3, benimPid: 1 }) === false)
  kontrol('kilit: başlangıç anı öğrenilemiyor ama pid yaşıyor → güvenli taraf, canlı', sahipCanli({ pid: 7, bas }, { yasiyorMu: canli, baslangicOku: () => null, benimPid: 1 }) === true)
  kontrol('kilit: sahip ölü ama durdurulamamış araç yaşıyor → canlı', sahipCanli({ pid: 0, cocukPid: 9, cocukBas: bas }, { yasiyorMu: p => p === 9, baslangicOku: () => basMs, benimPid: 1 }) === true)
  kontrol('kilit: sahip ve araç ölü → canlı değil', sahipCanli({ pid: 7, cocukPid: 9, bas }, { yasiyorMu: olu, baslangicOku: () => basMs, benimPid: 1 }) === false)
  kontrol('yeniden başlatma: ilk kez → yeni', yenidenBaslatKarari(null, false).karar === 'yeni')
  kontrol('yeniden başlatma: önceki kilit var ama kayıt yok/bozuk → reddet (T4)', yenidenBaslatKarari(null, true).karar === 'reddet' && yenidenBaslatKarari({}, true).karar === 'reddet')
  kontrol('yeniden başlatma: bitmiş iş → zaten-bitti', yenidenBaslatKarari({ durum: 'bitti', sessionId: 'a' }, true).karar === 'zaten-bitti')
  kontrol('yeniden başlatma: kimlik varsa (hata/durduruldu/giriş dahil) → devam', ['calisiyor', 'kota-bekliyor', 'hata', 'durduruldu', 'giris'].every(d => yenidenBaslatKarari({ durum: d, sessionId: 'a' }, true).karar === 'devam'))
  kontrol('yeniden başlatma: kimliksiz + açıkça adım yok → yeni', yenidenBaslatKarari({ durum: 'calisiyor', sessionId: null, adimVar: false }, true).karar === 'yeni')
  kontrol('yeniden başlatma: kimliksiz + adım var ya da BİLİNMİYOR → reddet', yenidenBaslatKarari({ durum: 'calisiyor', adimVar: true }, true).karar === 'reddet' && yenidenBaslatKarari({ durum: 'calisiyor' }, true).karar === 'reddet')
  kontrol('yeniden başlatma: bir kez "yeniden başlatılamaz" işaretlenen iş hep reddedilir', yenidenBaslatKarari({ durum: 'hata', sessionId: 'a', yenidenBaslatilamaz: true }, true).karar === 'reddet')
  const argC = komutHazirla(null, { arac: 'claude', sahte: 'x.mjs' }).args.join(' ')
  kontrol('Claude işi: kullanılabilir araçlar --tools ile daraltıldı, Bash ve WebFetch açıkça yasak, hesap eklentileri kapalı (T3)',
    argC.includes('--tools Read,Grep,Glob,Edit,Write,TodoWrite,WebSearch') && argC.includes('--disallowedTools Bash,WebFetch') && argC.includes('--strict-mcp-config'), argC.slice(-200))
  kontrol('yeniden deneme: kimlik varsa evet; kimlik yok + adım yoksa evet; kimlik yok + adım varsa HAYIR',
    yenidenDenenebilir({ sessionId: 'a', adimVar: true }) && yenidenDenenebilir({ sessionId: null, adimVar: false }) && !yenidenDenenebilir({ sessionId: null, adimVar: true }))
  kontrol('sınıflandırma: Claude "API Error" metni sadece başarı yoksa → giriş', siniflandir({ apiMetni: 'API Error: 401 OAuth token has expired', kod: 1 }).tur === 'giris'
    && siniflandir({ apiMetni: 'API Error 429 örneğini belgeledim', sonTerminal: B, kod: 0 }).tur === 'bitti')
  // Claude araması: masaüstü uygulaması yoksa yerel kurucunun yeri, sonra PATH (sadece .exe)
  const { claudeBul } = await import(pathToFileURL(NOBET).href)
  const ev = path.join(KLASOR, 'claude-bul-ev'), yolKlasoru = path.join(KLASOR, 'claude-bul-yol'), yok = path.join(KLASOR, 'claude-bul-yok')
  fs.mkdirSync(path.join(ev, '.local', 'bin'), { recursive: true }); fs.mkdirSync(yolKlasoru, { recursive: true })
  fs.writeFileSync(path.join(ev, '.local', 'bin', 'claude.exe'), '')
  fs.writeFileSync(path.join(yolKlasoru, 'claude.cmd'), '')
  kontrol('Claude araması: masaüstü uygulaması yoksa %USERPROFILE%\\.local\\bin\\claude.exe',
    claudeBul({ APPDATA: yok, PATH: yolKlasoru }, ev) === path.join(ev, '.local', 'bin', 'claude.exe'))
  kontrol('Claude araması: PATH\'te yalnızca claude.cmd varsa bulunmaz (sadece .exe)', claudeBul({ APPDATA: yok, PATH: yolKlasoru }, yok) === null)
  fs.writeFileSync(path.join(yolKlasoru, 'claude.exe'), '')
  kontrol('Claude araması: PATH\'teki (tırnaklı) klasörde claude.exe', claudeBul({ APPDATA: yok, PATH: `"${yolKlasoru}"` }, yok) === path.join(yolKlasoru, 'claude.exe'))
}
console.log(hata ? `${hata} test KALDI, ${gecen} geçti` : `hepsi geçti (${gecen})`)
process.exit(hata ? 1 : 0)
