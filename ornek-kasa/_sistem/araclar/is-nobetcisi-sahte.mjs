// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
// İş nöbetçisi için sahte Claude/Codex: gerçek kota harcamadan test etmeye yarar. Mod ortam değişkeninden gelir.
// İstemi, gerçek araçlar gibi standart girişten (stdin) okur.
//   kota-sonra-bitti  : ilk çağrıda kota hatası (birkaç saniye sonra açılır), devam çağrısında başarı
//   kota-uzun         : her çağrıda 60 sn sonra açılan kota (iptal / "Şimdi dene" testleri için)
//   giris             : giriş gerekli hatası
//   basari-stderr-429 : iş başarılı ama hata akışında "429 rate limit … try again at 3:09 AM" gibi sıradan metin var
//   basari-kod1       : başarı olayı geliyor ama araç 1 koduyla çıkıyor
//   bos-kod0          : hiçbir tamamlanma bilgisi yok, çıkış 0
//   uzun-calis        : 30 sn çalışır, sonra başarı (çift başlatma ve çalışırken iptal testleri için)
//   gecici-kota-basari-kod1 : önce geçici kota hatası, sonra tur başarıyla biter ama araç 1 koduyla çıkar (kota SAYILMAMALI)
//   adim-kimliksiz-kota     : oturum kimliği vermeden bir iş adımı atar, sonra kota (baştan başlatılMAMALI)
// Codex mi Claude mu olduğunu argümanlardan anlar (Codex çağrısı "exec" ile başlar).
const a = process.argv.slice(2)
const mod = process.env.SAHTE_MOD || 'kota-sonra-bitti'
const yaz = o => process.stdout.write(JSON.stringify(o) + '\n')
let istem = ''
process.stdin.setEncoding('utf8')
process.stdin.on('data', d => { istem += d })
process.stdin.on('end', calis)

async function calis() {
  const codex = a[0] === 'exec'
  if (mod === 'adim-kimliksiz-kota') {
    const sn = Math.floor(Date.now() / 1000) + 3
    if (codex) {
      yaz({ type: 'item.started', item: { type: 'command_execution', command: 'dir' } })
      yaz({ type: 'turn.failed', error: { message: 'You’ve hit your usage limit. try again in 3 seconds.' } })
    } else {
      yaz({ type: 'assistant', message: { content: [{ type: 'tool_use', name: 'Edit', input: { file_path: 'x.md' } }] } })
      yaz({ type: 'result', is_error: true, result: 'Claude AI usage limit reached|' + sn })
    }
    process.exit(1)
  }
  const devam = codex ? a.includes('resume') : a.includes('--resume')
  const kimlik = devam ? a[a.indexOf(codex ? 'resume' : '--resume') + 1] : (codex ? 'sahte-codex-1' : 'sahte-oturum-1')
  const devamIstemi = istem.startsWith('Kullanım hakkın dolduğu')
  if (codex) {
    yaz({ type: 'thread.started', thread_id: kimlik }); yaz({ type: 'turn.started' })
    if (!a.includes('workspace-write') || a.at(-1) !== '-') { yaz({ type: 'turn.failed', error: { message: 'argüman yanlış' } }); process.exit(1) }
  } else {
    yaz({ type: 'system', subtype: 'init', session_id: kimlik })
    yaz({ type: 'assistant', message: { content: [{ type: 'tool_use', name: 'Read', input: { file_path: 'AGENTS.md' } }] } })
  }
  const basari = metin => codex
    ? (yaz({ type: 'item.completed', item: { type: 'agent_message', text: metin } }), yaz({ type: 'turn.completed', usage: {} }))
    : yaz({ type: 'result', is_error: false, result: metin })
  const hata = metin => codex
    ? (yaz({ type: 'error', message: metin }), yaz({ type: 'turn.failed', error: { message: metin } }))
    : yaz({ type: 'result', is_error: true, result: metin })

  if (mod === 'giris') { hata(codex ? 'Not logged in. Please run codex login.' : 'OAuth token has expired. Please run /login'); process.exit(1) }
  if (mod === 'basari-stderr-429') {
    process.stderr.write('Belge notu: HTTP 429 rate limit örneği; try again at 3:09 AM; unauthorized örneği\n')
    basari('bitti'); process.exit(0)
  }
  if (mod === 'basari-kod1') { basari('bitti ama'); process.exit(1) }
  if (mod === 'gecici-kota-basari-kod1') {
    if (codex) yaz({ type: 'error', message: 'You’ve hit your usage limit. try again in 3 seconds.' })
    basari('bitti'); process.exit(1)
  }
  if (mod === 'bos-kod0') process.exit(0)
  if (mod === 'uzun-calis') { await new Promise(r => setTimeout(r, 30000)); basari('bitti'); process.exit(0) }
  if (mod === 'kota-uzun' || !devam) {
    const sn = mod === 'kota-uzun' ? 60 : 3
    hata(codex ? `You’ve hit your usage limit. try again in ${sn} seconds.` : `Claude AI usage limit reached|${Math.floor(Date.now() / 1000) + sn}`)
    process.exit(1)
  }
  basari(`bitti; devam istemi alındı: ${devamIstemi ? 'evet' : 'hayır'}`)
  process.exit(0)
}
