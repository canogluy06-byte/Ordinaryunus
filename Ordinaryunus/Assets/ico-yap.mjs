// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
// Packs PNG images into a Windows .ico. No dependencies (Node built-ins only).
//
// Build:  node ico-yap.mjs logo.ico f-16.png f-24.png f-32.png f-48.png f-64.png f-128.png f-256.png
//         add --png-all to store every size as PNG (smaller file, but see note below)
// Check:  node ico-yap.mjs --check logo.ico
//
// Layout: ICONDIR (6 bytes) + one 16-byte ICONDIRENTRY per image + payloads, smallest first.
// Payloads: 256 px is stored as PNG (standard since Windows Vista); smaller sizes are stored as
// classic 32-bit BMP/DIB frames (BGRA + AND mask). Reason: some readers, e.g. System.Drawing on
// .NET Framework (tested with Windows PowerShell 5.1), misread PNG frames below 256 px.
import { readFileSync, writeFileSync } from 'node:fs';
import { inflateSync } from 'node:zlib';

const PNG_SIG = '89504e470d0a1a0a';

// Decode an 8-bit RGBA or RGB, non-interlaced PNG into straight-alpha RGBA bytes.
function decodePng(buf) {
  let p = 8, w, h, depth, ctype, interlace; const idat = [];
  while (p < buf.length) {
    const len = buf.readUInt32BE(p), type = buf.toString('latin1', p + 4, p + 8), d = buf.subarray(p + 8, p + 8 + len);
    if (type === 'IHDR') { w = d.readUInt32BE(0); h = d.readUInt32BE(4); depth = d[8]; ctype = d[9]; interlace = d[12]; }
    else if (type === 'IDAT') idat.push(d);
    else if (type === 'IEND') break;
    p += 12 + len;
  }
  if (depth !== 8 || (ctype !== 6 && ctype !== 2) || interlace) throw new Error('only 8-bit RGB/RGBA non-interlaced PNG is supported');
  const bpp = ctype === 6 ? 4 : 3, stride = w * bpp, raw = inflateSync(Buffer.concat(idat));
  const px = Buffer.alloc(h * stride);
  for (let y = 0; y < h; y++) {
    const f = raw[y * (stride + 1)], src = y * (stride + 1) + 1, dst = y * stride;
    for (let x = 0; x < stride; x++) {
      const a = x >= bpp ? px[dst + x - bpp] : 0, b = y ? px[dst - stride + x] : 0, c = x >= bpp && y ? px[dst - stride + x - bpp] : 0;
      let v = raw[src + x];
      if (f === 1) v += a; else if (f === 2) v += b; else if (f === 3) v += (a + b) >> 1;
      else if (f === 4) { const pp = a + b - c, pa = Math.abs(pp - a), pb = Math.abs(pp - b), pc = Math.abs(pp - c); v += pa <= pb && pa <= pc ? a : pb <= pc ? b : c; }
      px[dst + x] = v & 255;
    }
  }
  if (bpp === 4) return { w, h, rgba: px };
  const rgba = Buffer.alloc(w * h * 4);
  for (let i = 0; i < w * h; i++) { px.copy(rgba, i * 4, i * 3, i * 3 + 3); rgba[i * 4 + 3] = 255; }
  return { w, h, rgba };
}

// 32-bit DIB icon frame: BITMAPINFOHEADER (height doubled) + bottom-up BGRA + 1-bit AND mask.
function toDib({ w, h, rgba }) {
  const maskStride = Math.ceil(w / 32) * 4;
  const out = Buffer.alloc(40 + w * h * 4 + maskStride * h);
  out.writeUInt32LE(40, 0); out.writeInt32LE(w, 4); out.writeInt32LE(h * 2, 8);
  out.writeUInt16LE(1, 12); out.writeUInt16LE(32, 14); out.writeUInt32LE(w * h * 4, 20);
  for (let y = 0; y < h; y++) {
    const row = h - 1 - y; // bottom-up
    for (let x = 0; x < w; x++) {
      const s = (y * w + x) * 4, d = 40 + (row * w + x) * 4;
      out[d] = rgba[s + 2]; out[d + 1] = rgba[s + 1]; out[d + 2] = rgba[s]; out[d + 3] = rgba[s + 3];
      if (rgba[s + 3] === 0) out[40 + w * h * 4 + row * maskStride + (x >> 3)] |= 0x80 >> (x & 7); // 1 = transparent
    }
  }
  return out;
}

function check(file) {
  const b = readFileSync(file);
  const [reserved, type, count] = [b.readUInt16LE(0), b.readUInt16LE(2), b.readUInt16LE(4)];
  if (reserved !== 0 || type !== 1) throw new Error('not an .ico file');
  console.log(`${file}: ${b.length} bytes, ${count} images`);
  for (let i = 0; i < count; i++) {
    const e = 6 + i * 16;
    const w = b[e] || 256, h = b[e + 1] || 256, bpp = b.readUInt16LE(e + 6);
    const size = b.readUInt32LE(e + 8), off = b.readUInt32LE(e + 12);
    const png = b.toString('hex', off, off + 8) === PNG_SIG;
    const pw = png ? b.readUInt32BE(off + 16) : b.readInt32LE(off + 4);
    const ph = png ? b.readUInt32BE(off + 20) : b.readInt32LE(off + 8) / 2;
    const ok = pw === w && ph === h && off + size <= b.length;
    console.log(`  #${i} ${w}x${h} ${bpp}bpp ${png ? 'png' : 'dib'} ${pw}x${ph} ${size} B @${off} ${ok ? 'OK' : 'MISMATCH'}`);
  }
}

function build(out, files, pngAll) {
  const imgs = files.map(f => {
    const data = readFileSync(f);
    if (data.toString('hex', 0, 8) !== PNG_SIG) throw new Error(`${f} is not a PNG`);
    const w = data.readUInt32BE(16), h = data.readUInt32BE(20);
    if (w > 256 || h > 256) throw new Error(`${f} is larger than 256 px`);
    return { w, h, data: pngAll || w >= 256 ? data : toDib(decodePng(data)) };
  }).sort((a, b) => a.w - b.w);

  const header = Buffer.alloc(6 + imgs.length * 16);
  header.writeUInt16LE(0, 0);            // reserved
  header.writeUInt16LE(1, 2);            // type 1 = icon
  header.writeUInt16LE(imgs.length, 4);  // image count
  let offset = header.length;
  imgs.forEach((img, i) => {
    const e = 6 + i * 16;
    header[e] = img.w === 256 ? 0 : img.w;      // 0 means 256
    header[e + 1] = img.h === 256 ? 0 : img.h;
    header[e + 2] = 0;                          // palette colours (none)
    header[e + 3] = 0;                          // reserved
    header.writeUInt16LE(1, e + 4);             // colour planes
    header.writeUInt16LE(32, e + 6);            // bits per pixel (RGBA)
    header.writeUInt32LE(img.data.length, e + 8);
    header.writeUInt32LE(offset, e + 12);
    offset += img.data.length;
  });
  writeFileSync(out, Buffer.concat([header, ...imgs.map(i => i.data)]));
  check(out);
}

const args = process.argv.slice(2), pngAll = args.includes('--png-all');
const [first, ...rest] = args.filter(a => a !== '--png-all');
if (first === '--check') check(rest[0]);
else if (first && rest.length) build(first, rest, pngAll);
else console.log('usage: node ico-yap.mjs <out.ico> <png...> [--png-all]  |  node ico-yap.mjs --check <file.ico>');
