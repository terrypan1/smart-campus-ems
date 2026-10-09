<template>
  <div class="login">
    <!-- 左：品牌區（窄螢幕隱藏） -->
    <section class="hero gt-sm">
      <img class="hero-art" src="/images/login-campus.jpg" alt="" aria-hidden="true" />
      <div class="hero-shade" />

      <div class="hero-body column no-wrap">
        <div class="row items-center no-wrap brand">
          <span class="brand-logo"><q-icon :name="mdiLightningBolt" size="34px" /></span>
          <span class="brand-text">示範大學<span class="brand-sep">｜</span>能源管理系統 EMS</span>
        </div>

        <div class="hero-main">
          <h1 class="hero-title">看見每一度電，<br />管理每一分<em>能源</em>。</h1>
          <p class="hero-desc">智慧電表即時監控、用電分析、需量管理、<br />電費試算與碳排追蹤，一個畫面掌握校園能源。</p>

          <div class="flow">
            <template v-for="(s, i) in FLOW" :key="s.label">
              <div v-if="i > 0" class="flow-arrow">→</div>
              <div class="flow-step">
                <q-icon :name="s.icon" size="30px" />
                <span>{{ s.label }}</span>
              </div>
            </template>
          </div>

          <div class="stats row no-wrap">
            <div v-for="s in STATS" :key="s.label" class="stat row items-center no-wrap">
              <q-icon :name="s.icon" size="34px" class="stat-icon" />
              <div>
                <div class="stat-value num">{{ s.value }}</div>
                <div class="stat-label">{{ s.label }}</div>
              </div>
            </div>
          </div>
        </div>

        <div class="hero-foot"><span class="foot-line" />主校區 ◦ Demo</div>
      </div>
    </section>

    <!-- 右：登入表單 -->
    <section class="form-side column items-center justify-center">
      <q-form class="form" @submit.prevent="submit">
        <div class="form-title">登入</div>
        <div class="form-sub">請輸入帳號密碼</div>

        <label class="field-label" for="login-account">帳號</label>
        <q-input
          v-model.trim="account"
          for="login-account"
          outlined
          placeholder="請輸入帳號"
          aria-label="帳號"
          autocomplete="username"
          autofocus
          :disable="loading"
          class="field"
        >
          <template #prepend><q-icon :name="mdiAccountOutline" size="22px" /></template>
        </q-input>

        <label class="field-label" for="login-password">密碼</label>
        <q-input
          v-model="password"
          for="login-password"
          outlined
          placeholder="請輸入密碼"
          aria-label="密碼"
          :type="showPwd ? 'text' : 'password'"
          autocomplete="current-password"
          :disable="loading"
          class="field"
        >
          <template #prepend><q-icon :name="mdiLockOutline" size="22px" /></template>
          <template #append>
            <q-icon :name="showPwd ? mdiEyeOff : mdiEye" class="cursor-pointer" :aria-label="showPwd ? '隱藏密碼' : '顯示密碼'" @click="showPwd = !showPwd" />
          </template>
        </q-input>

        <div v-if="error" class="error">{{ error }}</div>

        <q-btn type="submit" class="full-width submit btn-primary" unelevated no-caps :loading="loading">
          <span>登入</span>
          <q-icon :name="mdiArrowRight" size="20px" class="q-ml-sm" />
        </q-btn>
      </q-form>

      <q-btn flat dense no-caps class="theme-btn" :icon="mode === 'dark' ? mdiWeatherSunny : mdiWeatherNight" :label="mode === 'dark' ? '淺色' : '深色'" @click="toggle" />
    </section>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  mdiEye, mdiEyeOff, mdiWeatherSunny, mdiWeatherNight, mdiLightningBolt, mdiAccountOutline, mdiLockOutline, mdiArrowRight,
  mdiMeterElectricOutline, mdiSitemapOutline, mdiServerOutline, mdiDatabaseOutline, mdiMonitorDashboard,
  mdiOfficeBuildingOutline, mdiClockOutline,
} from '@quasar/extras/mdi-v7'
import { $login } from '@/api'
import { errorMessage } from '@/api/http'
import { Notify } from 'quasar'
import { auth, saveAuth } from '@/stores/auth'
import { HOME_PATH } from '@/router/menu'
import { useTheme } from '@/theme/useTheme'

// 資料流程：跟 docs/實作/01 的架構一致
const FLOW = [
  { icon: mdiMeterElectricOutline, label: '智慧電表' },
  { icon: mdiSitemapOutline, label: 'Modbus TCP/IP' },
  { icon: mdiServerOutline, label: '資料採集' },
  { icon: mdiDatabaseOutline, label: 'Database' },
  { icon: mdiMonitorDashboard, label: 'EMS Web' },
]
// 數字依電表主檔（data/meters.json：20 棟、51 顆）與採集間隔（60 秒）
const STATS = [
  { icon: mdiMeterElectricOutline, value: '51', label: '智慧電表' },
  { icon: mdiOfficeBuildingOutline, value: '20', label: '棟建築' },
  { icon: mdiClockOutline, value: '1', label: '分鐘採集' },
]

const route = useRoute()
const router = useRouter()
const { mode, toggle } = useTheme()

const account = ref('')
const password = ref('')
const showPwd = ref(false)
const loading = ref(false)
const error = ref('')

async function submit() {
  if (!account.value || !password.value) {
    error.value = '請輸入帳號與密碼'
    return
  }
  loading.value = true
  error.value = ''
  try {
    const res = await $login(account.value, password.value)
    saveAuth(res.data!)
  } catch (e) {
    error.value = errorMessage(e, '登入失敗')
    loading.value = false
    return
  }

  // 登入成功：提示＋換頁。換頁失敗（例如部署後分頁檔換了）就整頁跳轉，不能停在登入頁沒反應
  Notify.create({ type: 'positive', message: `登入成功，歡迎 ${auth.user?.displayName ?? ''}`, timeout: 1500 })
  const redirect = typeof route.query.redirect === 'string' && route.query.redirect.startsWith('/') ? route.query.redirect : HOME_PATH
  try {
    const failure = await router.replace(redirect)
    if (failure) window.location.assign(redirect)
  } catch {
    window.location.assign(redirect)
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.login {
  display: flex;
  min-height: 100vh;
  background: var(--page-bg);
}

/* ---------- 左：品牌區 ---------- */
.hero {
  position: relative;
  flex: 1 1 auto;
  min-width: 0;
  overflow: hidden;
  color: #f0fdfa;
  background:
    radial-gradient(70% 55% at 85% 0%, rgba(94, 234, 212, 0.35) 0%, transparent 60%),
    linear-gradient(160deg, #0f6f63 0%, #0a5147 45%, #073b34 100%);
}
.hero-art {
  position: absolute;
  top: 0;
  right: 0;
  height: 100%;
  width: 58%;
  object-fit: cover;
  object-position: center;
  /* 左緣用遮罩漸隱、融進底色，不會有一條接縫 */
  -webkit-mask-image: linear-gradient(90deg, transparent 0%, rgba(0, 0, 0, 0.35) 18%, #000 45%);
  mask-image: linear-gradient(90deg, transparent 0%, rgba(0, 0, 0, 0.35) 18%, #000 45%);
}
/* 底部壓暗，頁尾文字讀得清楚 */
.hero-shade {
  position: absolute;
  inset: 0;
  background: linear-gradient(180deg, rgba(7, 59, 52, 0) 70%, rgba(7, 59, 52, 0.55) 100%);
}
.hero-body {
  position: relative;
  height: 100%;
  min-height: 100vh;
  padding: 44px 60px 36px;
}
.brand-logo {
  display: grid;
  place-items: center;
  width: 56px;
  height: 56px;
  border-radius: 14px;
  color: #fff;
  background: linear-gradient(145deg, #2dd4bf 0%, #0d9488 100%);
  box-shadow: 0 6px 18px rgba(13, 148, 136, 0.45);
}
.brand-text {
  margin-left: 22px;
  font-size: 26px;
  font-weight: 700;
  letter-spacing: 0.02em;
}
.brand-sep {
  margin: 0 12px;
  opacity: 0.6;
  font-weight: 400;
}
.hero-main {
  margin: auto 0;
  padding: 32px 0;
}
.hero-title {
  margin: 0;
  font-size: clamp(44px, 4.4vw, 70px);
  line-height: 1.3;
  font-weight: 800;
  letter-spacing: 0.02em;
}
.hero-title em {
  font-style: normal;
  color: #6ee7b7;
}
.hero-desc {
  margin: 22px 0 0;
  font-size: clamp(16px, 1.25vw, 20px);
  line-height: 1.75;
  color: rgba(240, 253, 250, 0.85);
}
.flow {
  display: inline-flex;
  align-items: center;
  gap: 14px;
  margin-top: 40px;
  padding: 18px 26px;
  border-radius: 14px;
  background: rgba(255, 255, 255, 0.06);
  border: 1px solid rgba(255, 255, 255, 0.16);
  -webkit-backdrop-filter: blur(6px);
  backdrop-filter: blur(6px);
}
.flow-step {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  min-width: 76px;
  font-size: 14px;
}
.flow-arrow {
  opacity: 0.55;
  font-size: 16px;
}
.stats {
  margin-top: 48px;
}
.stat {
  gap: 16px;
  padding: 0 36px;
  border-left: 1px solid rgba(255, 255, 255, 0.2);
}
.stat:first-child {
  padding-left: 6px;
  border-left: none;
}
.stat-icon {
  color: #6ee7b7;
}
.stat-value {
  font-size: 38px;
  font-weight: 700;
  line-height: 1.1;
}
.stat-label {
  margin-top: 4px;
  font-size: 16px;
  color: rgba(240, 253, 250, 0.85);
}
.hero-foot {
  display: flex;
  align-items: center;
  gap: 14px;
  font-size: 14px;
  color: rgba(240, 253, 250, 0.7);
}
.foot-line {
  width: 26px;
  height: 1px;
  background: currentColor;
}

/* 深色：同一張插畫壓暗，底色改成近黑的墨綠 */
html[data-theme='dark'] .hero {
  background:
    radial-gradient(70% 55% at 85% 0%, rgba(45, 212, 191, 0.16) 0%, transparent 60%),
    linear-gradient(160deg, #0c1513 0%, #070b0a 100%);
  border-right: 1px solid var(--chrome-border);
}
html[data-theme='dark'] .hero-art {
  filter: brightness(0.62) saturate(0.85);
}
html[data-theme='dark'] .hero-shade {
  background: linear-gradient(180deg, rgba(7, 11, 10, 0) 65%, rgba(7, 11, 10, 0.7) 100%);
}

/* ---------- 右：表單 ---------- */
.form-side {
  position: relative;
  flex: 0 0 31%;
  min-width: 440px;
  padding: 24px;
  overflow: hidden;
}
/* 右側背景的大圓弧裝飾 */
.form-side::before,
.form-side::after {
  content: '';
  position: absolute;
  border-radius: 50%;
  pointer-events: none;
}
.form-side::before {
  width: 520px;
  height: 520px;
  top: -200px;
  right: -260px;
  background: radial-gradient(circle, rgba(13, 148, 136, 0.06) 0%, transparent 70%);
  border: 1px solid rgba(13, 148, 136, 0.06);
}
.form-side::after {
  width: 640px;
  height: 640px;
  bottom: -360px;
  right: -200px;
  background: rgba(255, 255, 255, 0.55);
  border: 1px solid rgba(15, 23, 42, 0.04);
}
html[data-theme='dark'] .form-side::before {
  background: radial-gradient(circle, rgba(45, 212, 191, 0.06) 0%, transparent 70%);
  border-color: rgba(255, 255, 255, 0.05);
}
html[data-theme='dark'] .form-side::after {
  background: rgba(255, 255, 255, 0.02);
  border-color: rgba(255, 255, 255, 0.05);
}
@media (max-width: 1023px) {
  .form-side {
    flex: 1 1 auto;
    min-width: 0;
  }
}
.form {
  position: relative;
  z-index: 1;
  width: 100%;
  max-width: 424px;
  padding: 40px 36px 44px;
  border-radius: 18px;
  background: var(--card-sheen), var(--card-bg);
  border: 1px solid var(--card-border);
  box-shadow: 0 12px 40px rgba(15, 23, 42, 0.08), 0 2px 6px rgba(15, 23, 42, 0.04);
}
html[data-theme='dark'] .form {
  box-shadow: var(--card-shadow);
}
.form-title {
  font-size: 32px;
  font-weight: 800;
  line-height: 1.2;
  color: var(--text-1);
}
.form-sub {
  margin: 10px 0 34px;
  font-size: 17px;
  color: var(--text-2);
}
.field-label {
  display: block;
  margin-bottom: 10px;
  font-size: 16px;
  font-weight: 600;
  color: var(--text-1);
}
.field {
  margin-bottom: 26px;
}
.field :deep(.q-field__control) {
  height: 56px;
  border-radius: 10px;
}
.field :deep(.q-field__prepend) {
  padding-right: 14px;
  color: var(--text-3);
}
.field :deep(input) {
  font-size: 16px;
}
.submit {
  height: 56px;
  margin-top: 6px;
  border-radius: 10px;
  font-size: 18px;
  font-weight: 700;
  letter-spacing: 0.1em;
}
.error {
  margin: -8px 0 18px;
  padding: 8px 12px;
  border-radius: var(--radius-sm);
  font-size: 13px;
  color: var(--st-alarm-error-text);
  background: var(--st-alarm-error-soft);
}
.theme-btn {
  position: relative;
  z-index: 1;
  margin-top: 28px;
  font-size: 15px;
  color: var(--text-2);
}
</style>
