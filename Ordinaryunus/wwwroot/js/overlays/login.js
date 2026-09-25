// SPDX-License-Identifier: GPL-3.0-only
// Ordinaryunus · © 2026 Yunus Emre Canoğlu · GPL-3.0-only + ek şartlar (EK-SARTLAR.md) · https://github.com/canogluy06-byte/Ordinaryunus
import { request } from "../bridge.js";
import { icon } from "../ui.js";
import { IMZA, imzaSatiri } from "../imza.js";

export function initLogin(auth, onUnlock) {
  const card = document.getElementById("login-card");
  const form = document.getElementById("login-form");
  const passInput = document.getElementById("login-pass");
  const repeatWrap = document.getElementById("login-repeat-wrap");
  const repeatInput = document.getElementById("login-repeat");
  const errorEl = document.getElementById("login-error");
  const submitBtn = document.getElementById("login-submit");
  const eyeBtn = document.getElementById("login-eye");
  const forgotBtn = document.getElementById("login-forgot");

  const isNew = !auth.hasPassword;
  repeatWrap.classList.toggle("hidden", !isNew);
  submitBtn.textContent = isNew ? "Şifre belirle" : "Giriş yap";
  card.querySelector("h1").nextElementSibling.textContent = isNew ? "İlk açılış: kendi şifreni belirle." : "Senin ikinci beynin, tek ekranda.";
  fillSignature();

  let waitTimer = null;
  function setWait(seconds) {
    clearInterval(waitTimer);
    if (seconds <= 0) { errorEl.textContent = ""; submitBtn.disabled = false; return; }
    submitBtn.disabled = true;
    let s = seconds;
    errorEl.className = "login-wait";
    errorEl.textContent = `Çok denendi. ${s} saniye bekle…`;
    waitTimer = setInterval(() => {
      s--;
      if (s <= 0) { clearInterval(waitTimer); errorEl.textContent = ""; submitBtn.disabled = false; }
      else errorEl.textContent = `Çok denendi. ${s} saniye bekle…`;
    }, 1000);
  }
  if (auth.waitSeconds > 0) setWait(auth.waitSeconds);

  eyeBtn.addEventListener("click", () => {
    const showing = passInput.type === "text";
    passInput.type = showing ? "password" : "text";
    if (!repeatWrap.classList.contains("hidden")) repeatInput.type = passInput.type;
    eyeBtn.innerHTML = "";
    eyeBtn.appendChild(icon(showing ? "eye" : "eye-off"));
  });

  forgotBtn.addEventListener("click", () => {
    errorEl.className = "login-error";
    errorEl.textContent = "Uygulamayı kapat, %APPDATA%\\Ordinaryunus\\ayarlar.json dosyasını sil, yeni şifre belirle.";
  });

  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    errorEl.className = "login-error";
    errorEl.textContent = "";
    const pass = passInput.value;
    if (!pass) return;
    submitBtn.disabled = true;
    try {
      let res;
      if (isNew) {
        const repeat = repeatInput.value;
        if (pass.length < 6) { errorEl.textContent = "Şifre en az 6 karakter olmalı."; submitBtn.disabled = false; return; }
        res = await request("setPassword", { password: pass, repeat });
      } else {
        res = await request("login", { password: pass });
      }
      passInput.value = ""; if (repeatInput) repeatInput.value = "";
      if (res.unlocked) onUnlock();
    } catch (err) {
      passInput.value = "";
      card.classList.remove("shake"); void card.offsetWidth; card.classList.add("shake");
      if (err.code === "auth_wait") setWait(parseInt((err.message || "").match(/\d+/)?.[0] || "30", 10));
      else errorEl.textContent = err.message || "Bir şeyler ters gitti.";
    } finally {
      submitBtn.disabled = false;
    }
  });

  setTimeout(() => passInput.focus(), 200);
}

// Giriş ekranının altındaki yapımcı imzası: "<ad> tarafından yapıldı · v<sürüm>".
// Değerler tek kaynaktan (imza.js) gelir; burada elle ad ya da sürüm yazılmaz.
function fillSignature() {
  const el = document.getElementById("login-imza");
  if (!el) return;
  el.textContent = "";
  const ad = document.createElement("span");
  ad.className = "login-imza-ad";
  ad.textContent = IMZA.yapan;
  const ayrac = document.createElement("span");
  ayrac.className = "login-imza-ayrac";
  ayrac.setAttribute("aria-hidden", "true");
  ayrac.textContent = "·";
  el.append(ad, " tarafından yapıldı ", ayrac, " v" + IMZA.surum);
  el.title = imzaSatiri();
}
