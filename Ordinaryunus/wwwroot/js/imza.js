// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
// Uygulamanın imzası (arayüz tarafı). C# tarafındaki karşılığı Imza.cs; --selftest ikisinin aynı olduğunu denetler.
// Lisansın ek şartları (EK-SARTLAR.md, GPL-3.0 madde 7) gereği bu dosyadaki yazar bilgisi silinemez ve değiştirilemez;
// giriş ekranındaki ve Hakkında kartındaki yazar atfı da korunmalıdır.
export const IMZA = Object.freeze({
  yapan: 'Yunus Emre Canoğlu',
  yil: 2026,
  surum: '2.1.0',
  github: 'https://github.com/canogluy06-byte/Ordinaryunus',
  lisans: 'GPL-3.0',
});

/** Tek satırlık imza: "Ordinaryunus 2.1.0 · © 2026 Yunus Emre Canoğlu · GPL-3.0 lisansı" */
export const imzaSatiri = () => `Ordinaryunus ${IMZA.surum} · © ${IMZA.yil} ${IMZA.yapan} · ${IMZA.lisans} lisansı`;
