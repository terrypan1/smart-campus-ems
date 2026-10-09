import { createApp } from 'vue'
import { Quasar, Dialog, Notify, LoadingBar } from 'quasar'
import iconSet from 'quasar/icon-set/svg-mdi-v7'
import langZhTw from 'quasar/lang/zh-TW'

// 字型、icon 全部打包進來，內網不連外也能顯示
import '@fontsource-variable/inter'
import '@fontsource/noto-sans-tc/400.css'
import '@fontsource/noto-sans-tc/500.css'
import '@fontsource/noto-sans-tc/700.css'
import '@fontsource-variable/jetbrains-mono'
import 'quasar/src/css/index.sass'
import './theme/tokens.css'

import App from './App.vue'
import router from './router'
import { initTheme } from './theme/useTheme'

const app = createApp(App)
app.use(Quasar, {
  plugins: { Dialog, Notify, LoadingBar },
  iconSet,
  lang: langZhTw,
  config: {
    loadingBar: { color: 'primary', size: '3px', skipHijack: true },
    notify: { position: 'top-right', timeout: 2500 },
  },
})
app.use(router)
initTheme()
app.mount('#app')
