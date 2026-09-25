#!/usr/bin/env node
// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
// Codex köprüsü: Codex'e komut satırından iş verilmesini sağlar. İş nöbetçisi (is-nobetcisi.mjs) Codex işlerini
// bunun üzerinden başlatır.
//
// Neden gerekli?  Codex masaüstü uygulaması kendi codex.exe'sini getiriyor ama PATH'e eklemiyor.
// Üstelik klasör adında her güncellemede değişen bir kod var:
//   %LOCALAPPDATA%\OpenAI\Codex\bin\<değişen-kod>\codex.exe
// Bu betik en yeni codex.exe'yi kendisi bulur ve verdiğin argümanlarla çalıştırır.
// Masaüstü uygulaması yoksa npm ile kurulmuş Codex komut satırını (npm i -g @openai/codex) PATH'te arar.
//
// Kullanım (kasa klasöründe):
//   node _sistem/araclar/codex.mjs --version
//   node _sistem/araclar/codex.mjs exec -C "C:\Kasam" -s workspace-write "görev metni"
//   node _sistem/araclar/codex.mjs yol        -> sadece codex.exe'nin yerini yazar

import fs from 'node:fs'
import path from 'node:path'
import { spawn } from 'node:child_process'

function codexBul() {
  const aday = []
  const kok = path.join(process.env.LOCALAPPDATA || '', 'OpenAI', 'Codex', 'bin')
  try {
    for (const d of fs.readdirSync(kok)) {
      const exe = path.join(kok, d, 'codex.exe')
      if (fs.existsSync(exe)) aday.push({ exe, zaman: fs.statSync(exe).mtimeMs })
    }
  } catch {}
  aday.sort((a, b) => b.zaman - a.zaman)
  if (aday.length) return aday[0].exe
  // Yedek: ayrı kurulmuş komut satırı (npm i -g @openai/codex) PATH'teyse onu kullan
  if (process.platform !== 'win32') return 'codex'
  // npm'in giriş dosyasını doğrudan bul: cmd.exe tırnak içindeki kullanıcı girdisini bile genişletir, ondan kaçınılır
  for (const entry of (process.env.PATH || '').split(path.delimiter)) {
    const dir = entry.replace(/^"|"$/g, '')
    if (!dir) continue
    const native = path.join(dir, 'codex.exe')
    if (fs.existsSync(native)) return native
    const script = path.join(dir, 'node_modules', '@openai', 'codex', 'bin', 'codex.js')
    if (fs.existsSync(script)) return script
  }
  throw new Error('Codex bulunamadı. Codex masaüstü uygulamasını veya npm ile kurulan Codex komut satırını kontrol et.')
}

let exe
try { exe = codexBul() } catch (e) { console.error(e.message); process.exit(1) }
const argumanlar = process.argv.slice(2)
if (argumanlar[0] === 'yol') { console.log(exe); process.exit(0) }

// Argümanlar kabuk olmadan geçirilir: tırnaklar, yüzde işaretleri ve boş metinler olduğu gibi kalır
const cocuk = exe.endsWith('.js')
  ? spawn(process.execPath, [exe, ...argumanlar], { stdio: 'inherit', shell: false })
  : spawn(exe, argumanlar, { stdio: 'inherit', shell: false })
cocuk.on('error', e => { console.error(`Codex bulunamadı ya da çalışmadı: ${e.message}\nCodex masaüstü uygulaması kurulu mu?`); process.exit(1) })
cocuk.on('exit', kod => process.exit(kod ?? 1))
